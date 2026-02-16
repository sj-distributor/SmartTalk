using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<RealtimeAiFunctionCallResult> OnFunctionCallAsync(
        SessionBusinessContext ctx,
        RealtimeAiWssFunctionCallData functionCallData,
        RealtimeAiSessionActions actions,
        CancellationToken cancellationToken)
    {
        Log.Information("[AiSpeechAssistantConnect] Function call received: {FunctionName}, Args: {Args}",
            functionCallData.FunctionName, functionCallData.ArgumentsJson);

        return functionCallData.FunctionName switch
        {
            OpenAiToolConstants.ConfirmOrder => ProcessOrder(ctx, functionCallData),
            OpenAiToolConstants.ConfirmCustomerInformation => ProcessRecordCustomerInformation(ctx, functionCallData),
            OpenAiToolConstants.ConfirmPickupTime => ProcessRecordOrderPickupTime(ctx, functionCallData),
            OpenAiToolConstants.Hangup => ProcessHangup(ctx, cancellationToken),
            OpenAiToolConstants.RepeatOrder or OpenAiToolConstants.SatisfyOrder =>
                await ProcessRepeatOrderAsync(ctx, actions, cancellationToken).ConfigureAwait(false),
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
                ProcessTransferCall(ctx, functionCallData.FunctionName, cancellationToken),
            _ => null
        };
    }

    private static RealtimeAiFunctionCallResult ProcessOrder(
        SessionBusinessContext ctx, RealtimeAiWssFunctionCallData functionCallData)
    {
        ctx.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(functionCallData.ArgumentsJson);

        return new RealtimeAiFunctionCallResult
        {
            Output = "Please confirm the order content with the customer. If this is the first time confirming, repeat the order details. Once the customer confirms, do not repeat the details again. " +
                     "If the order is confirmed, we will proceed with asking for the pickup time and will no longer repeat the order details."
        };
    }

    private static RealtimeAiFunctionCallResult ProcessRecordCustomerInformation(
        SessionBusinessContext ctx, RealtimeAiWssFunctionCallData functionCallData)
    {
        ctx.UserInfo = JsonConvert.DeserializeObject<AiSpeechAssistantUserInfoDto>(functionCallData.ArgumentsJson);

        return new RealtimeAiFunctionCallResult
        {
            Output = "Reply in the guest's language: OK, I've recorded it for you."
        };
    }

    private static RealtimeAiFunctionCallResult ProcessRecordOrderPickupTime(
        SessionBusinessContext ctx, RealtimeAiWssFunctionCallData functionCallData)
    {
        var parsed = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(functionCallData.ArgumentsJson);
        if (ctx.OrderItems != null)
            ctx.OrderItems.Comments = parsed?.Comments ?? string.Empty;

        return new RealtimeAiFunctionCallResult
        {
            Output = "Record the time when the customer pickup the order."
        };
    }

    private RealtimeAiFunctionCallResult ProcessHangup(SessionBusinessContext ctx, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Schedule<IAiSpeechAssistantService>(
            x => x.HangupCallAsync(ctx.CallSid, cancellationToken), TimeSpan.FromSeconds(2));

        return new RealtimeAiFunctionCallResult
        {
            Output = "Say goodbye to the guests in their **language**"
        };
    }

    private async Task<RealtimeAiFunctionCallResult> ProcessRepeatOrderAsync(
        SessionBusinessContext ctx, RealtimeAiSessionActions actions, CancellationToken cancellationToken)
    {
        actions.SuspendClientAudioToProvider();

        try
        {
            await SendRepeatOrderHoldOnAudioAsync(ctx, actions).ConfigureAwait(false);

            var recordedAudio = await actions.GetRecordedAudioSnapshotAsync().ConfigureAwait(false);

            if (recordedAudio is { Length: > 0 })
            {
                var audioData = BinaryData.FromBytes(recordedAudio);

                var responseAudio = await _openaiClient.GenerateAudioChatCompletionAsync(
                    audioData,
                    ctx.Assistant.CustomRepeatOrderPrompt,
                    ctx.Assistant.ModelVoice,
                    cancellationToken).ConfigureAwait(false);

                var uLawAudioBytes = await _ffmpegService.ConvertWavToULawAsync(responseAudio, cancellationToken).ConfigureAwait(false);

                await actions.SendAudioToClientAsync(Convert.ToBase64String(uLawAudioBytes)).ConfigureAwait(false);
            }
        }
        finally
        {
            actions.ResumeClientAudioToProvider();
        }

        return null;
    }

    private static async Task SendRepeatOrderHoldOnAudioAsync(
        SessionBusinessContext ctx, RealtimeAiSessionActions actions)
    {
        Enum.TryParse(ctx.Assistant.ModelVoice, true, out AiSpeechAssistantVoice voice);
        voice = voice == default ? AiSpeechAssistantVoice.Alloy : voice;

        Enum.TryParse(ctx.Assistant.ModelLanguage, true, out AiSpeechAssistantMainLanguage language);
        language = language == default ? AiSpeechAssistantMainLanguage.En : language;

        var stream = AudioHelper.GetRandomAudioStream(voice, language);

        using var holdOnStream = new MemoryStream();
        await stream.CopyToAsync(holdOnStream).ConfigureAwait(false);
        var holdOn = Convert.ToBase64String(holdOnStream.ToArray());

        await actions.SendAudioToClientAsync(holdOn).ConfigureAwait(false);
    }

    private RealtimeAiFunctionCallResult ProcessTransferCall(
        SessionBusinessContext ctx, string functionName, CancellationToken cancellationToken)
    {
        Log.Information("Start transfer call");

        if (ctx.IsTransfer) return null;

        if (string.IsNullOrEmpty(ctx.HumanContactPhone))
        {
            return new RealtimeAiFunctionCallResult
            {
                Output = "Reply in the guest's language: I'm Sorry, there is no human service at the moment"
            };
        }

        ctx.IsTransfer = true;

        var (reply, replySeconds) = MatchTransferCallReply(functionName);

        Log.Information("Transfer call reply: {Reply}", reply);

        _backgroundJobClient.Schedule<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
        {
            CallSid = ctx.CallSid,
            HumanPhone = ctx.HumanContactPhone
        }, cancellationToken), TimeSpan.FromSeconds(replySeconds), HangfireConstants.InternalHostingTransfer);

        // V1 comments out the function output send for transfer calls — returning null skips it
        return null;
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
