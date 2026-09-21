using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeAiFunctionCallResult ProcessConfirmOrder(RealtimeAiWssFunctionCallData functionCallData)
    {
        if (!TryParseFunctionCallArguments<AiSpeechAssistantOrderDto>(functionCallData.ArgumentsJson, functionCallData.FunctionName, _ctx.CallSid, out var order))
            return BuildArgumentRecoveryResult(functionCallData);

        _ctx.OrderItems = order;
        ReleaseArgumentRecovery(_ctx.ArgumentRecoveryClaims, functionCallData.FunctionName);

        return new RealtimeAiFunctionCallResult
        {
            Output = "Please confirm the order content with the customer. If this is the first time confirming, repeat the order details. Once the customer confirms, do not repeat the details again. " +
                     "Here is the current order: {context.OrderItemsJson}. If the order is confirmed, we will proceed with asking for the pickup time and will no longer repeat the order details."
        };
    }

    private RealtimeAiFunctionCallResult ProcessConfirmCustomerInformation(RealtimeAiWssFunctionCallData functionCallData)
    {
        if (!TryParseFunctionCallArguments<AiSpeechAssistantUserInfoDto>(functionCallData.ArgumentsJson, functionCallData.FunctionName, _ctx.CallSid, out var userInfo))
            return BuildArgumentRecoveryResult(functionCallData);

        _ctx.UserInfo = userInfo;
        ReleaseArgumentRecovery(_ctx.ArgumentRecoveryClaims, functionCallData.FunctionName);

        return new RealtimeAiFunctionCallResult
        {
            Output = "Reply in the guest's language: OK, I've recorded it for you."
        };
    }

    private RealtimeAiFunctionCallResult ProcessConfirmPickupTime(RealtimeAiWssFunctionCallData functionCallData)
    {
        if (!TryParseFunctionCallArguments<AiSpeechAssistantOrderDto>(functionCallData.ArgumentsJson, functionCallData.FunctionName, _ctx.CallSid, out var parsed))
            return BuildArgumentRecoveryResult(functionCallData);

        ReleaseArgumentRecovery(_ctx.ArgumentRecoveryClaims, functionCallData.FunctionName);

        if (_ctx.OrderItems != null) _ctx.OrderItems.Comments = parsed?.Comments ?? string.Empty;

        return new RealtimeAiFunctionCallResult
        {
            Output = "Record the time when the customer pickup the order."
        };
    }

    /// <summary>
    /// The reply for a tool whose arguments could not be read, or null to leave today's behaviour in
    /// place once that tool has spent its one attempt. Null returns are what the engine already does
    /// with a discarded reply, so nothing is sent and the idle hangup stays reachable.
    /// </summary>
    private RealtimeAiFunctionCallResult BuildArgumentRecoveryResult(RealtimeAiWssFunctionCallData functionCallData)
    {
        if (!TryClaimArgumentRecovery(_ctx.ArgumentRecoveryClaims, functionCallData.FunctionName, functionCallData.CallId)) return null;

        return new RealtimeAiFunctionCallResult { Output = ArgumentRecoveryOutput };
    }
}
