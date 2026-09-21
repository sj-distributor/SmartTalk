using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    /// <summary>
    /// The reply a tool gets when the model's arguments could not be read: steer the assistant back to
    /// the customer rather than into another attempt at the same tool. Worded to stop the loop — a
    /// "try again" reading produces a tight, audio-free exchange the caller hears as silence.
    /// </summary>
    private const string ArgumentRecoveryOutput =
        "You did not receive that clearly. Tell the customer in their own language that you did not catch it and ask them to say it again. " +
        "Do not call this tool again until the customer has answered.";

    /// <summary>
    /// Deserializes a tool's arguments, reporting failure instead of throwing.
    ///
    /// <para>A throw here reaches the engine's per-handler catch, which discards that tool's reply; when
    /// it is the only call in the batch nothing triggers a response and the caller hears nothing until
    /// the idle follow-up hangs up. Static so the failure cases are testable without constructing the
    /// service, following <c>FormatCustomerPhone</c> and <c>CanRecordCall</c>.</para>
    ///
    /// <para>Covers the throwing subset only. An empty, blank or literal <c>"null"</c> payload
    /// deserializes to null WITHOUT throwing, so it reports success and assigns null exactly as it did
    /// before — unchanged behaviour, deliberately.</para>
    /// </summary>
    internal static bool TryParseFunctionCallArguments<T>(string argumentsJson, string functionName, string callSid, out T parsed) where T : class
    {
        try
        {
            parsed = JsonConvert.DeserializeObject<T>(argumentsJson);
            return true;
        }
        catch (Exception ex)
        {
            // No payload: the arguments are the customer's order and their own words about it, and the
            // exception already carries the JSON path and position, which is the diagnostic half.
            Log.Warning(ex, "[AiAssistant] Function call arguments unreadable, FunctionName: {FunctionName}, CallSid: {CallSid}, ArgumentChars: {ArgumentChars}",
                functionName, callSid, argumentsJson?.Length ?? 0);

            parsed = null;
            return false;
        }
    }

    /// <summary>
    /// Claims a tool's single recovery reply for this call, or refuses when it is already spent.
    ///
    /// <para>Replying to an unreadable tool call is what turns dead air into the assistant asking the
    /// customer to repeat themselves. Replying to it EVERY time is what removes the call's only
    /// terminator: the discarded reply is why exactly one turn completes today, and completing a turn
    /// restarts the idle countdown from zero, so an unbounded recovery against a model that re-emits
    /// the same arguments produces a call that never ends. There is no session ceiling on the phone
    /// path to catch that.</para>
    ///
    /// <para>Refuses without a call id: a reply needs one to address, and a rejected reply on a socket
    /// that is no longer open is classified critical and drops the call. Sending nothing is what
    /// happens today and is the safe answer.</para>
    /// </summary>
    internal static bool TryClaimArgumentRecovery(HashSet<string> claimed, string functionName, string callId) =>
        !string.IsNullOrEmpty(callId) && claimed.Add(functionName);

    /// <summary>Returns a tool's recovery after it parses cleanly again, so one transient malformed payload does not spend it for the rest of the call.</summary>
    internal static void ReleaseArgumentRecovery(HashSet<string> claimed, string functionName) => claimed.Remove(functionName);

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
            OpenAiToolConstants.CollectComplaintInfo => ProcessCollectComplaintInfo(functionCallData),
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
