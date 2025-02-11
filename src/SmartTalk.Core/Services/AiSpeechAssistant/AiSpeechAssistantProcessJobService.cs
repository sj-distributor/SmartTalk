using System.Text;
using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Constants;
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
    Task RecordAiSpeechAssistantCallAsync(AiSpeechAssistantStreamContextDto context, CancellationToken cancellationToken);
}

public class AiSpeechAssistantProcessJobService : IAiSpeechAssistantProcessJobService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly TwilioSettings _twilioSettings;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;

    public AiSpeechAssistantProcessJobService(
        IMapper mapper,
        IVectorDb vectorDb,
        TwilioSettings twilioSettings,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _twilioSettings = twilioSettings;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
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
            TranscriptionText = FormattedConversation(context.ConversationTranscription),
            Language = TranscriptionLanguage.Chinese,
            CreatedDate = callResource.StartTime ?? DateTimeOffset.Now,
            OrderStatus = PhoneOrderOrderStatus.Pending
        };

        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync([record], cancellationToken: cancellationToken).ConfigureAwait(false);

        var conversations = ConvertToPhoneOrderConversations(context.ConversationTranscription, record.Id);

        await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(conversations, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (context.OrderItems != null)
            await OrderRestaurantItemsAsync(record, context.OrderItems, cancellationToken).ConfigureAwait(false);
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
        var order = 0;

        return conversationTranscription.Select(entry => new PhoneOrderConversation
            {
                RecordId = recordId,
                Question = entry.Item1 == AiSpeechAssistantSpeaker.Ai ? entry.Item2 : string.Empty,
                Answer = entry.Item1 == AiSpeechAssistantSpeaker.User ? entry.Item2 : string.Empty,
                Order = order++
            }).ToList();
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