using Mediator.Net;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeAiFunctionCallResult ProcessTransferCall(string functionName, CancellationToken cancellationToken)
    {
        if (_ctx.IsTransfer) return null;

        if (string.IsNullOrEmpty(_ctx.HumanContactPhone))
        {
            Log.Information("[AiAssistant] Transfer unavailable, no human contact, CallSid: {CallSid}", _ctx.CallSid);
            return new RealtimeAiFunctionCallResult
            {
                Output = "Reply in the guest's language: I'm Sorry, there is no human service at the moment"
            };
        }

        _ctx.IsTransfer = true;

        var (reply, replySeconds) = MatchTransferCallReply(functionName);

        Log.Information("[AiAssistant] Transferring to human, Function: {Function}, Phone: {Phone}, CallSid: {CallSid}",
            functionName, _ctx.HumanContactPhone, _ctx.CallSid);

        _backgroundJobClient.Schedule<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
        {
            CallSid = _ctx.CallSid,
            HumanPhone = _ctx.HumanContactPhone
        }, cancellationToken), TimeSpan.FromSeconds(replySeconds), HangfireConstants.InternalHostingTransfer);

        return new RealtimeAiFunctionCallResult { Output = reply };
    }

    private static (string Reply, int ReplySeconds) MatchTransferCallReply(string functionName)
    {
        return functionName switch
        {
            OpenAiToolConstants.TransferCall =>
                ("Reply in the guest's language: I'm transferring you to a human customer service representative.", 2),
            OpenAiToolConstants.HandleThirdPartyDelayedDelivery or OpenAiToolConstants.HandleThirdPartyFoodQuality or OpenAiToolConstants.HandleThirdPartyUnexpectedIssues =>
                ("Reply in the guest's language: I am deeply sorry for the inconvenience caused to you. I will transfer you to the relevant personnel for processing. Please wait.", 4),
            OpenAiToolConstants.HandlePhoneOrderIssues or OpenAiToolConstants.CheckOrderStatus or OpenAiToolConstants.HandleThirdPartyPickupTimeChange or OpenAiToolConstants.RequestOrderDelivery =>
                ("Reply in the guest's language: OK, I will transfer you to the relevant person for processing. Please wait.", 3),
            OpenAiToolConstants.HandlePromotionCalls =>
                ("Reply in the guest's language: I don't support business that is not related to the restaurant at the moment, and I will help you contact the relevant person for processing. Please wait.", 4),
            _ =>
                ("Reply in the guest's language: I'm transferring you to a human customer service representative.", 2)
        };
    }
}
