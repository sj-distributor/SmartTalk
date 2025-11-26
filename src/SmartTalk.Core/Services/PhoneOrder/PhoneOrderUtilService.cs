using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Core.Services.PhoneOrder;

public interface IPhoneOrderUtilService : IScopedDependency
{
    Task ExtractPhoneOrderShoppingCartAsync(string goalTexts, PhoneOrderRecord record, CancellationToken cancellationToken);
}

public class PhoneOrderUtilService : IPhoneOrderUtilService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiiSpeechAssistantDataProvider;

    public PhoneOrderUtilService(IMapper mapper, IVectorDb vectorDb, ISmartiesClient smartiesClient,
        IPosDataProvider posDataProvider, IPhoneOrderDataProvider phoneOrderDataProvider, IRedisSafeRunner redisSafeRunner, IRestaurantDataProvider restaurantDataProvider, IAiSpeechAssistantDataProvider aiiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _smartiesClient = smartiesClient;
        _posDataProvider = posDataProvider;
        _redisSafeRunner = redisSafeRunner;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
        _aiiSpeechAssistantDataProvider = aiiSpeechAssistantDataProvider;
    }

    public async Task ExtractPhoneOrderShoppingCartAsync(string goalTexts, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        try
        {
            var shoppingCart = await GetOrderDetailsAsync(goalTexts, cancellationToken).ConfigureAwait(false);
            
            var (assistant, agent) = await _aiiSpeechAssistantDataProvider.GetAgentAndAiSpeechAssistantAsync(
                record.AgentId, record.AssistantId, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            Log.Information("Get ai speech assistant: {@Assistant} and agent: {@Agent} by agentId: {AgentId}, assistantId: {AssistantId}", assistant, agent, record.AgentId, record.AssistantId);

            if (!record.AssistantId.HasValue) assistant = null;

            if (assistant is not { IsAutoGenerateOrder: true }) return;
            
            var posAgents = await _posDataProvider.GetPosAgentsAsync(agentId: record.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);

            Log.Information("Get the pos agent: {@PosAgents} by agent id: {AgentId}", posAgents, record.AgentId);
        
            var items = posAgents != null && posAgents.Count != 0
                ? await MatchSimilarProductsAsync(record, shoppingCart, cancellationToken).ConfigureAwait(false)
                : await GetSimilarRestaurantByRecordAsync(record, shoppingCart, cancellationToken).ConfigureAwait(false);

            if (items.Count != 0)
                await _phoneOrderDataProvider.AddPhoneOrderItemAsync(items, true, cancellationToken).ConfigureAwait(false);
            
            if (assistant is { IsAllowOrderPush: true })
            {
                switch (agent.Type)
                {
                    case AgentType.Sales:
                        await HandleSalesOrderAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case AgentType.PosCompanyStore:
                        await HandlePosOrderAsync(cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("Match similar items failed: {@Exception}", e);
        }
    }
    
    public async Task<PhoneOrderDetailDto> GetOrderDetailsAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync( new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款高度理解语言的智能助手，根据所有对话提取Client的food_details。" +
                                       "--规则：" +
                                       "1.根据全文帮我提取food_details，count是菜品的数量且为整数，如果你不清楚数量的时候，count默认为1，remark是对菜品的备注" +
                                       "2.根据对话中Client的话为主提取food_details" +
                                       "3.不要出现重复菜品，如果有特殊的要求请标明数量，例如我要两份粥，一份要辣，则标注一份要辣" +
                                       "注意用json格式返回；规则：{\"food_details\": [{\"food_name\": \"菜品名字\",\"count\":减少的数量（负数）, \"remark\":null}]}}" +
                                       "-样本与输出：" +
                                       "input: Restaurant: . Client:Hi, 我可以要一個外賣嗎? Restaurant:可以啊,要什麼? Client: 我要幾個特價午餐,要一個蒙古牛,要一個蛋花湯跟這個,再要一個椒鹽排骨蛋花湯,然後再要一個魚香肉絲,不要辣的蛋花湯。Restaurant:可以吧。Client:然后再要一个春卷 再要一个法式柠檬柳粒。out:{\"food_details\": [{\"food_name\":\"蒙古牛\",\"count\":1, \"remark\":null},{\"food_name\":\"蛋花湯\",\"count\":3, \"remark\":},{\"food_name\":\"椒鹽排骨\",\"count\":1, \"remark\":null},{\"food_name\":\"魚香肉絲\",\"count\":1, \"remark\":null},{\"food_name\":\"春卷\",\"count\":1, \"remark\":null},{\"food_name\":\"法式柠檬柳粒\",\"count\":1, \"remark\":null}]}" +
                                       "input: Restaurant: Moon house Client: Hi, may I please have a compound chicken with steamed white rice? Restaurant: Sure, 10 minutes, thank you. Client: Hold on, I'm not finished, I'm not finished Restaurant: Ok, Sir, First Sir, give me your phone number first One minute, One minute, One minute, One minute, Ok, One minute, One minute Client: Okay Restaurant: Ok, 213 Client: 590-6995 You guys want me to order something for you guys? Restaurant: 295, Rm Client: 590-2995 Restaurant: Ah, no, yeah, maybe they have an old one, so, that's why. Client: Okay, come have chicken with cream white rice Restaurant: Bye bye, okay, something else? Client: Good morning, Kidman Restaurant: Okay Client: What do you want?  An order of mongolian beef also with cream white rice please Restaurant: Client: Do you want something, honey?  No, on your plate, you want it?  Let's go to the level, that's a piece of meat.  Let me get an order of combination fried rice, please. Restaurant: Sure, Question is how many wires do we need? Client: Maverick, do you want to share a chicken chow mein with me, for later?  And a chicken chow mein, please.  So that's one compote chicken, one orange chicken, one mingolian beef, one combination rice, and one chicken chow mein, please. Restaurant: Okay, let's see how many, one or two Client: Moon house Restaurant: Tube Tuner, right? Client: Can you separate, can you put in a bag by itself, the combination rice and the mongolian beef with one steamed rice please, because that's for getting here with my daughter. Restaurant: Okay, so let me know.  Okay, so I'm going to leave it.  Okay.  Got it Client: Moon house Restaurant: I'll make it 20 minutes, OK?  Oh, I'm sorry, you want a Mangaloreng beef on a fried rice and one steamed rice separate, right?  Yes.  OK. Client: combination rice, the mongolian beans and the steamed rice separate in one bag. Restaurant: Okay, Thank you Thank you out:{\"food_details\":[{\"food_name\":\"compound chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"orange chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"mongolian beef\",\"count\":1, \"remark\":null},{\"food_name\":\"chicken chow mein\",\"count\":1, \"remark\":null},{\"food_name\":\"combination rice\",\"count\":1, \"remark\":null},{\"food_name\":\"white rice\",\"count\":2, \"remark\":null}]}"
                                       )
                    
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input:{query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o,
            ResponseFormat = new () { Type = "json_object" }
        }, cancellationToken).ConfigureAwait(false);
        
        return completionResult.Data.Response == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(completionResult.Data.Response);
    }

    private async Task<List<PhoneOrderOrderItem>> GetSimilarRestaurantByRecordAsync(PhoneOrderRecord record, PhoneOrderDetailDto foods, CancellationToken cancellationToken)
    {
        if (record == null || foods?.FoodDetails == null || foods.FoodDetails.Count == 0) return [];
        
        var restaurant = await _restaurantDataProvider.GetRestaurantByNameAsync(record.RestaurantInfo?.Name, cancellationToken).ConfigureAwait(false);
        
        if (restaurant == null) return [];
        
        var tasks = foods.FoodDetails.Where(x => !string.IsNullOrWhiteSpace(x?.FoodName)).Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                restaurant.Id.ToString(), foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedRestaurantPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;
            
            foodDetail.FoodName = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Name;
            foodDetail.Price = (double)JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Price;
            foodDetail.ProductId = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).ProductId;
            
            return foodDetail;
        }).ToList();

        var result = await Task.WhenAll(tasks);

        return result.Where(fd => fd != null).Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodName,
            Quantity = int.TryParse(x.Count, out var parsedValue) ? parsedValue : 1,
            Price = x.Price,
            Note = x.Remark,
            ProductId = x.ProductId
        }).ToList();
    }
    
    private async Task<List<PhoneOrderOrderItem>> MatchSimilarProductsAsync(PhoneOrderRecord record, PhoneOrderDetailDto foods, CancellationToken cancellationToken)
    {
        if (record == null || foods?.FoodDetails == null || foods.FoodDetails.Count == 0) return [];
        
        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Generate pos order for store: {@Store} by agentId: {AgentId}", store, record.AgentId);
        
        if (store == null) return [];

        var tasks = foods.FoodDetails.Where(x => !string.IsNullOrWhiteSpace(x?.FoodName)).Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                $"pos-{store.Id}", foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedPosProductPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;

            var productPayload = JsonConvert.DeserializeObject<PosProductPayloadDto>(payload);
            foodDetail.FoodName = productPayload.Names;
            foodDetail.Price = (double)productPayload.Price;
            foodDetail.ProductId = productPayload.ProductId;
            
            return new SimilarResult
            {
                Id = productPayload.Id,
                FoodDetail = foodDetail,
                LanguageCode = productPayload.LanguageCode
            };
        }).ToList();

        var completedTasks = await Task.WhenAll(tasks);

        var results = completedTasks.Where(x => x != null && x.Id != 0).ToList();
        
        await BuildPosOrderAsync(record, store, results, cancellationToken).ConfigureAwait(false);
        
        return results.Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodDetail.FoodName,
            Quantity = int.TryParse(x.FoodDetail.Count, out var parsedValue) ? parsedValue : 1,
            Price = x.FoodDetail.Price,
            Note = x.FoodDetail.Remark,
            ProductId = x.FoodDetail.ProductId
        }).ToList();
    }

    private async Task BuildPosOrderAsync(PhoneOrderRecord record, CompanyStore store, List<SimilarResult> similarResults, CancellationToken cancellationToken)
    {
        var products = await _posDataProvider.GetPosProductsAsync(
            storeId: store.Id, ids: similarResults.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        var taxes = GetOrderItemTaxes(products, similarResults);
        
        await _redisSafeRunner.ExecuteWithLockAsync($"generate-order-number-{store.Id}", async() =>
        {
            var orderNo = await GenerateOrderNumberAsync(store, cancellationToken).ConfigureAwait(false);
            
            var order = new PosOrder
            {
                StoreId = store.Id,
                Name = record?.CustomerName ?? "Unknown",
                Phone = record?.PhoneNumber ?? "Unknown",
                OrderNo = orderNo,
                Status = PosOrderStatus.Pending,
                Count = products.Count,
                Tax = taxes,
                Total = products.Sum(p => p.Price),
                SubTotal = products.Sum(p => p.Price) + taxes,
                Type = PosOrderReceiveType.Pickup,
                Items = BuildPosOrderItems(products, similarResults),
                Notes = record?.Comments ?? string.Empty,
                RecordId = record!.Id
            };
            
            Log.Information("Generate complete order: {@Order}", order);
        
            await _posDataProvider.AddPosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
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

    private decimal GetOrderItemTaxes(List<PosProduct> products, List<SimilarResult> similarResults)
    {
        decimal taxes = 0;
        var productMap = new Dictionary<PosProduct, int>();
        
        foreach (var product in products)
        {
            var result = similarResults.Where(x => x.Id == product.Id).FirstOrDefault();

            if (result == null ) continue;
            
            productMap.Add(product, int.TryParse(result.FoodDetail.Count, out var parsedValue) ? parsedValue : 1);
        }
        
        foreach (var (product, quantity) in productMap)
        {
            var productTaxes = JsonConvert.DeserializeObject<List<EasyPosResponseTax>>(product.Tax);
            
            var productTax = productTaxes?.FirstOrDefault()?.Value;

            taxes += productTax.HasValue ? product.Price * quantity * (productTax.Value / 100) : 0;
            
            var modifiers = !string.IsNullOrEmpty(product.Modifiers) ? JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers) : [];

            taxes += modifiers.Sum(modifier => modifier.ModifierProducts.Sum(x => (x?.Price ?? 0) * ((modifier.Taxes?.FirstOrDefault()?.Value ?? 0) / 100)));
        }
        
        Log.Information("Calculate order item taxes: {Taxes}", taxes);
        
        return taxes;
    }

    private string BuildPosOrderItems(List<PosProduct> products, List<SimilarResult> similarResults)
    {
        EnrichSimilarResults(products, similarResults);
        
        var orderItems = similarResults.Where(x => x.Product != null).Select(x => new PhoneCallOrderItem
        {
            ProductId = Convert.ToInt64(x.Product.ProductId),
            Quantity = int.TryParse(x.FoodDetail.Count, out var parsedValue) ? parsedValue : 1,
            OriginalPrice = x.Product.Price,
            Price = x.Product.Price,
            Notes = string.IsNullOrWhiteSpace(x.FoodDetail?.Remark) ? string.Empty : x.FoodDetail?.Remark,
            OrderItemModifiers = HandleSpecialItems(x.Product)
        }).Where(x => x.ProductId != 0).ToList();
        
        Log.Information("Generate order items: {@orderItems}", orderItems);
            
        return JsonConvert.SerializeObject(orderItems);
    }

    private void EnrichSimilarResults(List<PosProduct> products, List<SimilarResult> similarResults)
    {
        foreach (var productIdInfo in similarResults.Where(productIdInfo => products.Any(x => x.Id == productIdInfo.Id)))
        {
            productIdInfo.Product = products.First(x => x.Id == productIdInfo.Id);
        }
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

    private async Task HandleSalesOrderAsync(CancellationToken cancellationToken)
    {
        // ToDo: Place order to hifood
    }
    
    private async Task HandlePosOrderAsync(CancellationToken cancellationToken)
    {
        // ToDo: Place order to pos
    }
}

public class SimilarResult
{
    public int Id { get; set; }
    
    public string LanguageCode { get; set; }
    
    public PosProduct Product { get; set; }
    
    public FoodDetailDto FoodDetail { get; set; }
}