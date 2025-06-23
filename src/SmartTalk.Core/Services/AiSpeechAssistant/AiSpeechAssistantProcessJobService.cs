using System.Text;
using AutoMapper;
using Newtonsoft.Json;
using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Services.Security;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Enums.STT;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using MessageReadRecord = SmartTalk.Core.Domain.Pos.MessageReadRecord;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantProcessJobService : IScopedDependency
{
    Task RecordAiSpeechAssistantCallAsync(AiSpeechAssistantStreamContextDto context, CancellationToken cancellationToken);
    
    Task OpenAiAccountTrainingAsync(OpenAiAccountTrainingCommand command, CancellationToken cancellationToken);
}

public class AiSpeechAssistantProcessJobService : IAiSpeechAssistantProcessJobService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly TwilioSettings _twilioSettings;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly OpenAiTrainingSettings _openAiTrainingSettings;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly OpenAiAccountTrainingSettings _openAiAccountTrainingSettings;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly ISecurityDataProvider _securityDataProvider;

    public AiSpeechAssistantProcessJobService(
        IMapper mapper,
        IVectorDb vectorDb,
        TwilioSettings twilioSettings,
        IPosDataProvider posDataProvider,
        IRedisSafeRunner redisSafeRunner,
        IRestaurantDataProvider restaurantDataProvider,
        OpenAiTrainingSettings openAiTrainingSettings, 
        OpenAiAccountTrainingSettings openAiAccountTrainingSettings,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IPhoneOrderService phoneOrderService,
        ISmartTalkHttpClientFactory httpClientFactory,
        ISecurityDataProvider securityDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _twilioSettings = twilioSettings;
        _posDataProvider = posDataProvider;
        _redisSafeRunner = redisSafeRunner;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _openAiTrainingSettings = openAiTrainingSettings;
        _restaurantDataProvider = restaurantDataProvider;
        _phoneOrderService = phoneOrderService;
        _openAiAccountTrainingSettings = openAiAccountTrainingSettings;
        _httpClientFactory = httpClientFactory;
        _securityDataProvider = securityDataProvider;
    }

    public async Task RecordAiSpeechAssistantCallAsync(AiSpeechAssistantStreamContextDto context, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        var callResource = await CallResource.FetchAsync(pathSid: context.CallSid).ConfigureAwait(false);

        var record = new PhoneOrderRecord
        {
            AgentId = context.Assistant.AgentId,
            SessionId = context.CallSid,
            Status = PhoneOrderRecordStatus.Transcription,
            Tips = context.ConversationTranscription.FirstOrDefault().Item2,
            TranscriptionText = string.Empty,
            Language = TranscriptionLanguage.Chinese,
            CreatedDate = callResource.StartTime ?? DateTimeOffset.Now,
            OrderStatus = PhoneOrderOrderStatus.Pending,
            CustomerName = context.UserInfo?.UserName,
            PhoneNumber = context.UserInfo?.PhoneNumber
        };

        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync([record], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await GenerateOrderItemsAsync(record, context.OrderItems, cancellationToken).ConfigureAwait(false);
        
        var roleUsers = await _securityDataProvider.GetRoleUserByPermissionNameAsync(permissionName: SecurityStore.Permissions.CanViewPhoneOrder, cancellationToken).ConfigureAwait(false);
        
        var messageReadRecords = roleUsers.Select(u => new MessageReadRecord()
        {
            RecordId = record.Id,
            UserId = u.UserId
        }).ToList();
        
        await _phoneOrderDataProvider.AddMessageReadRecordsAsync(messageReadRecords, true, cancellationToken).ConfigureAwait(false);
    }

    private static string FormattedConversation(List<(AiSpeechAssistantSpeaker, string)> conversationTranscription)
    {
        var formattedConversation = new StringBuilder();

        foreach (var entry in conversationTranscription)
        {
            var speaker = entry.Item1 == AiSpeechAssistantSpeaker.Ai ? "Restaurant" : "Customer";
            formattedConversation.AppendLine($"{speaker}: {entry.Item2}");
        }

        return formattedConversation.ToString();
    }

    private static List<PhoneOrderConversation> ConvertToPhoneOrderConversations(List<(AiSpeechAssistantSpeaker, string)> conversationTranscription, int recordId)
    {
        var conversations = new List<PhoneOrderConversation>();
        if (conversationTranscription == null || !conversationTranscription.Any()) return conversations;

        var order = 0;
        PhoneOrderConversation currentConversation = null;

        for (var i = 0; i < conversationTranscription.Count; i++)
        {
            var entry = conversationTranscription[i];
            var currentSpeaker = entry.Item1;
            var currentText = entry.Item2;

            if (currentConversation == null)
            {
                if (currentSpeaker == AiSpeechAssistantSpeaker.Ai)
                {
                    currentConversation = new PhoneOrderConversation
                    {
                        RecordId = recordId,
                        Question = currentText,
                        Answer = string.Empty,
                        Order = order++
                    };
                }
            }
            else
            {
                switch (currentSpeaker)
                {
                    case AiSpeechAssistantSpeaker.User when conversationTranscription[i - 1].Item1 == AiSpeechAssistantSpeaker.Ai:
                        currentConversation.Answer = currentText;
                        break;
                    case AiSpeechAssistantSpeaker.Ai when conversationTranscription[i - 1].Item1 == AiSpeechAssistantSpeaker.User:
                        conversations.Add(currentConversation);
                        currentConversation = new PhoneOrderConversation
                        {
                            RecordId = recordId,
                            Question = currentText,
                            Answer = string.Empty,
                            Order = order++
                        };
                        break;
                    case AiSpeechAssistantSpeaker.Ai:
                        currentConversation.Question += " " + currentText;
                        break;
                    default:
                        currentConversation.Answer += " " + currentText;
                        break;
                }
            }
        }

        if (currentConversation != null) conversations.Add(currentConversation);

        return conversations;
    }

    private async Task GenerateOrderItemsAsync(PhoneOrderRecord record, AiSpeechAssistantOrderDto foods, CancellationToken cancellationToken)
    {
        var posItems = await MatchSimilarProductsAsync(record, foods, cancellationToken).ConfigureAwait(false);
    
        Log.Information("Matched similar pos product items: {@PosItems}", posItems);
    }
    
    private async Task<List<PhoneOrderOrderItem>> MatchSimilarRestaurantItemsAsync(PhoneOrderRecord record, AiSpeechAssistantOrderDto foods, CancellationToken cancellationToken)
    {
        var result = new PhoneOrderDetailDto { FoodDetails = new List<FoodDetailDto>() };
        var restaurant = await _restaurantDataProvider.GetRestaurantByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);

        var tasks = _mapper.Map<PhoneOrderDetailDto>(foods).FoodDetails.Select(async foodDetail =>
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

        var completedTasks = await Task.WhenAll(tasks);
        
        result.FoodDetails.AddRange(completedTasks.Where(fd => fd != null));
        
        return result.FoodDetails.Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodName,
            Quantity = int.TryParse(x.Count, out var parsedValue) ? parsedValue : 1,
            Price = x.Price,
            Note = x.Remark,
            ProductId = x.ProductId
        }).ToList();
    }

    public async Task OpenAiAccountTrainingAsync(OpenAiAccountTrainingCommand command, CancellationToken cancellationToken)
    {
        var prompt = "生成3000字历史类论文，不要生成框架，要一篇完整的满3000字的论文";

        var client = new ChatClient("gpt-4o", _openAiTrainingSettings.ApiKey);
        var anotherClient = new ChatClient("gpt-4o", _openAiAccountTrainingSettings.ApiKey);

        var result = await client.CompleteChatAsync(prompt).ConfigureAwait(false);
        var anotherResult = await anotherClient.CompleteChatAsync(prompt).ConfigureAwait(false);

        var content = result?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        var anotherContent = anotherResult?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;

        var preview = string.IsNullOrEmpty(content) 
            ? "[内容为空]" 
            : content.Length > 50 ? content.Substring(0, 50) + "..." : content;

        var anotherPreview = string.IsNullOrEmpty(anotherContent) 
            ? "[内容为空]" 
            : anotherContent.Length > 50 ? anotherContent.Substring(0, 50) + "..." : anotherContent;

        Log.Information("OpenAiAccountTraining 主账号返回 (前50字): {Preview}（总长度: {Length}）", preview, content?.Length ?? 0);
        Log.Information("OpenAiAccountTraining 备用账号返回 (前50字): {Preview}（总长度: {Length}）", anotherPreview, anotherContent?.Length ?? 0);
    }
    
    private async Task<List<PhoneOrderOrderItem>> MatchSimilarProductsAsync(PhoneOrderRecord record, AiSpeechAssistantOrderDto foods, CancellationToken cancellationToken)
    {
        Log.Information("All ai order items: {@OrderItems}", foods);
        
        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Generate pos order for store: {@Store} by agentId: {AgentId}", store, record.AgentId);
        
        if (store == null) return [];

        var tasks = _mapper.Map<PhoneOrderDetailDto>(foods).FoodDetails.Where(x => !string.IsNullOrWhiteSpace(x?.FoodName)).Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                $"pos-{store.Id}", foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedPosProductPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;

            var productPayload = JsonConvert.DeserializeObject<PosProductPayloadDto>(payload);
            Log.Information("{FoodName}-similar product payload: {@Payload} ", foodDetail.FoodName, productPayload);
            
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
        
        Log.Information("All similar results: {@Results}", completedTasks);

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

    private async Task BuildPosOrderAsync(PhoneOrderRecord record, PosCompanyStore store, List<SimilarResult> similarResults, CancellationToken cancellationToken)
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

    private async Task<string> GenerateOrderNumberAsync(PosCompanyStore store, CancellationToken cancellationToken)
    {
        var preOrder = await _posDataProvider.GetPosOrderSortByOrderNoAsync(store.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (preOrder == null) return "0001";

        var rs = Convert.ToInt32(preOrder.OrderNo);
        
        rs++;
        
        return rs.ToString("D4");
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
}

public class SimilarResult
{
    public int Id { get; set; }
    
    public string LanguageCode { get; set; }
    
    public PosProduct Product { get; set; }
    
    public FoodDetailDto FoodDetail { get; set; }
}