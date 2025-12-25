using System.Text;
using AutoMapper;
using Google.Cloud.Translation.V2;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantProcessJobService : IScopedDependency
{
    Task SyncAiSpeechAssistantInfoToAgentAsync(SyncAiSpeechAssistantInfoToAgentCommand command, CancellationToken cancellationToken);
    
    Task RecordAiSpeechAssistantCallAsync(AiSpeechAssistantStreamContextDto context, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken);
}

public class AiSpeechAssistantProcessJobService : IAiSpeechAssistantProcessJobService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly TwilioSettings _twilioSettings;
    private readonly TranslationClient _translationClient;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IAiSpeechAssistantDataProvider _speechAssistantDataProvider;

    public AiSpeechAssistantProcessJobService(
        IMapper mapper,
        IVectorDb vectorDb,
        TwilioSettings twilioSettings,
        TranslationClient translationClient,
        IAgentDataProvider agentDataProvider,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IAiSpeechAssistantDataProvider speechAssistantDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _twilioSettings = twilioSettings;
        _translationClient = translationClient;
        _agentDataProvider = agentDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
        _speechAssistantDataProvider = speechAssistantDataProvider;
    }

    public async Task RecordAiSpeechAssistantCallAsync(AiSpeechAssistantStreamContextDto context, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        var callResource = await CallResource.FetchAsync(pathSid: context.CallSid).ConfigureAwait(false);
        
        var existRecord = await _phoneOrderDataProvider.GetPhoneOrderRecordBySessionIdAsync(context.CallSid, cancellationToken).ConfigureAwait(false);

        if (existRecord != null ) return;
        
        var agentAssistant = await _speechAssistantDataProvider.GetAgentAssistantsAsync(assistantIds: [context.Assistant.Id], cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agentAssistant == null || agentAssistant.Count == 0) throw new Exception("AgentAssistant is null");
        
        var parentRecordId = await _phoneOrderDataProvider.GetLatestPhoneOrderRecordIdAsync(agentAssistant.First().AgentId, context.Assistant.Id, context.CallSid, cancellationToken).ConfigureAwait(false);
        
        var record = new PhoneOrderRecord
        {
            AssistantId = context.Assistant.Id,
            AgentId = agentAssistant.First().AgentId,
            SessionId = context.CallSid,
            Status = PhoneOrderRecordStatus.Transcription,
            Tips = context.ConversationTranscription.FirstOrDefault().Item2,
            TranscriptionText = string.Empty,
            Language = TranscriptionLanguage.Chinese,
            CreatedDate = callResource.StartTime ?? DateTimeOffset.Now,
            OrderStatus = PhoneOrderOrderStatus.Pending,
            CustomerName = context.UserInfo?.UserName,
            PhoneNumber = context.UserInfo?.PhoneNumber,
            IsTransfer = context.IsTransfer,
            IncomingCallNumber = context.LastUserInfo.PhoneNumber,
            OrderRecordType = orderRecordType,
            ParentRecordId = parentRecordId
        };

        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync([record], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task SyncAiSpeechAssistantInfoToAgentAsync(SyncAiSpeechAssistantInfoToAgentCommand command, CancellationToken cancellationToken)
    {
        var agentAndAssistantPairs = await _speechAssistantDataProvider.GetAgentAndAiSpeechAssistantPairsAsync(cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting agent and assistant pairs: {AgentAndAssistantPairs}", agentAndAssistantPairs);
        
        foreach (var (agent, assistant) in agentAndAssistantPairs)
        {
            agent.IsSurface = true;
            agent.RelateId = agent.Id;
            agent.Name = assistant.Name;
            agent.Type = AgentType.Agent;
            agent.Voice = assistant.ModelVoice;
            agent.WaitInterval = assistant.WaitInterval;
            agent.IsTransferHuman = assistant.IsTransferHuman;
            agent.Channel = AiSpeechAssistantChannel.PhoneChat;
        }
        
        await _agentDataProvider.UpdateAgentsAsync(agentAndAssistantPairs.Select(x => x.Item1).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private async Task OrderRestaurantItemsAsync(PhoneOrderRecord record, AiSpeechAssistantOrderDto foods, CancellationToken cancellationToken)
    {
        var items = await MatchSimilarRestaurantItemsAsync(record, foods, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Matched similar restaurant items: {@items}", items);
        
        if (items.Count != 0)
            await _phoneOrderDataProvider.AddPhoneOrderItemAsync(items, true, cancellationToken).ConfigureAwait(false);
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
}