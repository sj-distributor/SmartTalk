using Newtonsoft.Json;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeAiFunctionCallResult ProcessConfirmOrder(RealtimeAiWssFunctionCallData functionCallData)
    {
        _ctx.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(functionCallData.ArgumentsJson);

        return new RealtimeAiFunctionCallResult
        {
            Output = "Please confirm the order content with the customer. If this is the first time confirming, repeat the order details. Once the customer confirms, do not repeat the details again. " +
                     "Here is the current order: {context.OrderItemsJson}. If the order is confirmed, we will proceed with asking for the pickup time and will no longer repeat the order details."
        };
    }

    private RealtimeAiFunctionCallResult ProcessConfirmCustomerInformation(RealtimeAiWssFunctionCallData functionCallData)
    {
        _ctx.UserInfo = JsonConvert.DeserializeObject<AiSpeechAssistantUserInfoDto>(functionCallData.ArgumentsJson);

        return new RealtimeAiFunctionCallResult
        {
            Output = "Reply in the guest's language: OK, I've recorded it for you."
        };
    }

    private RealtimeAiFunctionCallResult ProcessConfirmPickupTime(RealtimeAiWssFunctionCallData functionCallData)
    {
        var parsed = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(functionCallData.ArgumentsJson);
        
        if (_ctx.OrderItems != null) _ctx.OrderItems.Comments = parsed?.Comments ?? string.Empty;

        return new RealtimeAiFunctionCallResult
        {
            Output = "Record the time when the customer pickup the order."
        };
    }
}
