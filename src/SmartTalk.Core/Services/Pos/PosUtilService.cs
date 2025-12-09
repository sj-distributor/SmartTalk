using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Core.Services.Pos;

public interface IPosUtilService : IScopedDependency
{
    Task<PosOrder> BuildPosOrderAsync(PhoneOrderRecord record, AiDraftOrderDto aiDraftOrder, CancellationToken cancellationToken);
}

public class PosUtilService : IPosUtilService
{
    private readonly IMapper _mapper;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IRedisSafeRunner _redisSafeRunner;

    public PosUtilService(IMapper mapper, IPosDataProvider posDataProvider, IRedisSafeRunner redisSafeRunner)
    {
        _mapper = mapper;
        _posDataProvider = posDataProvider;
        _redisSafeRunner = redisSafeRunner;
    }

    public async Task<PosOrder> BuildPosOrderAsync(PhoneOrderRecord record, AiDraftOrderDto aiDraftOrder, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);
        
        var storeProducts = await _posDataProvider.GetPosProductsAsync(
            storeId: store.Id, productIds: aiDraftOrder.Items.Select(x => x.ProductId).ToList(), isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var distinctProducts = storeProducts.GroupBy(x => x.ProductId).Select(x => x.FirstOrDefault()).ToList();
        
        var draftMapping = BuildAiDraftAndProductMapping(distinctProducts, aiDraftOrder.Items);

        var taxes = GetOrderItemTaxes(draftMapping);
        
        return await _redisSafeRunner.ExecuteWithLockAsync($"generate-order-number-{store.Id}", async() =>
        {
            var items = BuildPosOrderItems(draftMapping);

            if (items.Count == 0) return null;
            
            var orderNo = await GenerateOrderNumberAsync(store, cancellationToken).ConfigureAwait(false);
            
            var order = new PosOrder
            {
                StoreId = store.Id,
                Name = record?.CustomerName ?? "Unknown",
                Phone = !string.IsNullOrWhiteSpace(record?.PhoneNumber) ? record.PhoneNumber : !string.IsNullOrWhiteSpace(record?.IncomingCallNumber) ? record.IncomingCallNumber.Replace("+1", "") : "Unknown",
                Address = record?.CustomerAddress,
                OrderNo = orderNo,
                Status = PosOrderStatus.Pending,
                Count = items.Sum(x => x.Quantity),
                Tax = taxes,
                Total = items.Sum(p => p.Price * p.Quantity) + taxes,
                SubTotal = items.Sum(p => p.Price * p.Quantity),
                Type = (PosOrderReceiveType)aiDraftOrder.Type,
                Items = JsonConvert.SerializeObject(items),
                Notes = record?.Comments ?? string.Empty,
                RecordId = record!.Id
            };
            
            Log.Information("Generate complete order: {@Order}", order);
        
            await _posDataProvider.AddPosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return order;
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
    }

    private List<(AiDraftItemDto Item, PosProduct Product)> BuildAiDraftAndProductMapping(List<PosProduct> products, List<AiDraftItemDto> items)
    {
        var mapping = new Dictionary<AiDraftItemDto, PosProduct>();
        
        foreach (var product in products)
        {
            var result = items.Where(x => x.ProductId == product.ProductId).FirstOrDefault();

            if (result == null ) continue;
            
            mapping.Add(result, product);
        }
        
        return mapping.Select(x => (x.Key, x.Value)).ToList();
    }

    private async Task<string> GenerateOrderNumberAsync(CompanyStore store, CancellationToken cancellationToken)
    {
        var (utcStart,utcEnd) = GetUtcMidnightForTimeZone(DateTimeOffset.UtcNow, store.Timezone);
        
        var preOrder = await _posDataProvider.GetPosOrderSortByOrderNoAsync(store.Id, utcStart, utcEnd, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (preOrder == null) return "0001";

        var rs = Convert.ToInt32(preOrder.OrderNo);
        
        rs++;
        
        return rs.ToString("D4");
    }
    
    private string TimezoneMapping(string timezone)
    {
        if (string.IsNullOrEmpty(timezone)) return "Pacific Standard Time";
        
        return timezone.Trim() switch
        {
            "America/Los_Angeles" => "Pacific Standard Time",
            _ => timezone.Trim()
        };
    }
    
    private (DateTimeOffset utcStart, DateTimeOffset utcEnd) GetUtcMidnightForTimeZone(DateTimeOffset utcNow, string timezone)
    {
        var windowsId = TimezoneMapping(timezone);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        
        var localTime = TimeZoneInfo.ConvertTime(utcNow, tz);
        var localMidnight = new DateTime(localTime.Year, localTime.Month, localTime.Day, 0, 0, 0);
        var localStart = new DateTimeOffset(localMidnight, tz.GetUtcOffset(localMidnight));
        
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);
        
        return (utcStart, utcEnd);
    }

    private decimal GetOrderItemTaxes(List<(AiDraftItemDto Item, PosProduct Product)> draftMapping)
    {
        decimal taxes = 0;
        
        foreach (var (aiDraft, product) in draftMapping)
        {
            var productTaxes = JsonConvert.DeserializeObject<List<EasyPosResponseTax>>(product.Tax);
            
            var productTax = productTaxes?.FirstOrDefault()?.Value;

            taxes += productTax.HasValue ? product.Price * aiDraft.Quantity * (productTax.Value / 100) : 0;
            
            var modifiers = !string.IsNullOrEmpty(product.Modifiers) ? JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers) : [];

            taxes += modifiers.Sum(modifier => modifier.ModifierProducts.Sum(x => (x?.Price ?? 0) * ((modifier.Taxes?.FirstOrDefault()?.Value ?? 0) / 100)));
        }
        
        Log.Information("Calculate order item taxes: {Taxes}", taxes);
        
        return taxes;
    }

    private List<PhoneCallOrderItem> BuildPosOrderItems(List<(AiDraftItemDto Item, PosProduct Product)> draftMapping)
    {
        var orderItems = draftMapping.Select(x => new PhoneCallOrderItem
        {
            ProductId = Convert.ToInt64(x.Product.ProductId),
            Quantity =x.Item.Quantity,
            OriginalPrice = x.Product.Price,
            Price = x.Product.Price,
            OrderItemModifiers = HandleSpecialItems(x.Product)
        }).Where(x => x.ProductId != 0).ToList();
        
        Log.Information("Generate order items: {@orderItems}", orderItems);
            
        return orderItems;
    }

    private List<PhoneCallOrderItemModifiers> HandleSpecialItems(PosProduct product)
    {
        var result = !string.IsNullOrWhiteSpace(product?.Modifiers) ? JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers) : [];

        if (result == null || result.Count == 0) return [];
        
        var orderItemModifiers = new List<PhoneCallOrderItemModifiers>();
        
        foreach (var modifierItem in result)
        {
            var items = modifierItem.ModifierProducts.Select(x => new PhoneCallOrderItemModifiers
            {
                Price = x?.Price ?? 0,
                Quantity = 1,
                ModifierId = modifierItem.Id,
                ModifierProductId = x?.Id ?? 0,
                Localizations = _mapper.Map<List<PhoneCallOrderItemLocalization>>(modifierItem.Localizations ?? []),
                ModifierLocalizations = _mapper.Map<List<PhoneCallOrderItemModifierLocalization>>(x?.Localizations ?? [])
            });
            
            orderItemModifiers.AddRange(items);
        }
        
        Log.Information("Generate order item: {@Product} modifiers: {@OrderItemModifiers}", product, orderItemModifiers);
        
        return orderItemModifiers;
    }
}