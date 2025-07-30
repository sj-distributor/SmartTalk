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

        var (existRecord, agent, aiSpeechAssistant) = await _phoneOrderDataProvider.GetRecordWithAgentAndAssistantAsync(context.CallSid, cancellationToken).ConfigureAwait(false);

        if (existRecord != null ) return;
        
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

    private async Task<List<PhoneOrderOrderItem>> GenerateOrderItemsAsync(PhoneOrderRecord record, AiSpeechAssistantOrderDto foods, CancellationToken cancellationToken)
    {
        try
        {
            var restaurantItems = await MatchSimilarRestaurantItemsAsync(record, foods, cancellationToken).ConfigureAwait(false);
        
            Log.Information("Matched similar restaurant items: {@RestaurantItems}", restaurantItems);
            
            var orderItems = restaurantItems != null && restaurantItems.Count != 0 ? restaurantItems : [];
            
            return orderItems.Where(x => !string.IsNullOrWhiteSpace(x.FoodName)).ToList();
        }
        catch (Exception e)
        {
            Log.Warning("Matched similar restaurant items failed: {@Exception}", e);

            return [];
        }
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
}