using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<RealtimeAiFunctionCallResult> OnFunctionCallAsync(
        RealtimeAiWssFunctionCallData functionCallData,
        RealtimeAiSessionActions actions,
        CancellationToken cancellationToken)
    {
        Log.Information("[AiAssistant] Function call received, Name: {FunctionName}, CallSid: {CallSid}", functionCallData.FunctionName, _ctx.CallSid);

        return functionCallData.FunctionName switch
        {
            OpenAiToolConstants.ConfirmOrder => ProcessConfirmOrder(functionCallData),
            OpenAiToolConstants.ConfirmCustomerInformation => ProcessConfirmCustomerInformation(functionCallData),
            OpenAiToolConstants.ConfirmPickupTime => ProcessConfirmPickupTime(functionCallData),
            OpenAiToolConstants.Hangup => ProcessHangup(cancellationToken),
            OpenAiToolConstants.RepeatOrder or OpenAiToolConstants.SatisfyOrder => await ProcessRepeatOrderAsync(actions, cancellationToken).ConfigureAwait(false),
            OpenAiToolConstants.Refund
                or OpenAiToolConstants.Complaint
                or OpenAiToolConstants.ReturnGoods
                or OpenAiToolConstants.TransferCall
                or OpenAiToolConstants.DeliveryTracking
                or OpenAiToolConstants.LessGoodsDelivered
                or OpenAiToolConstants.RefuseToAcceptGoods
                or OpenAiToolConstants.HandlePromotionCalls
                or OpenAiToolConstants.HandlePhoneOrderIssues
                or OpenAiToolConstants.CheckOrderStatus
                or OpenAiToolConstants.RequestOrderDelivery
                or OpenAiToolConstants.PickUpGoodsFromTheWarehouse
                or OpenAiToolConstants.HandleThirdPartyFoodQuality
                or OpenAiToolConstants.HandleThirdPartyDelayedDelivery
                or OpenAiToolConstants.HandleThirdPartyUnexpectedIssues
                or OpenAiToolConstants.HandleThirdPartyPickupTimeChange
                or OpenAiToolConstants.DriverDeliveryRelatedCommunication =>
                ProcessTransferCall(functionCallData.FunctionName),
            _ => null
        };
    }
}