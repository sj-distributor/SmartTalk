using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task HandleSessionReadyAsync(RealtimeAiSessionActions actions)
    {
        await SendOpeningGreetingOnceAsync(_ctx, actions).ConfigureAwait(false);
    }

    internal static async Task SendOpeningGreetingOnceAsync(
        AiSpeechAssistantConnectContext context,
        RealtimeAiSessionActions actions)
    {
        var greeting = context.Knowledge?.Greetings;
        if (string.IsNullOrEmpty(greeting)) return;

        if (Interlocked.CompareExchange(ref context.OpeningGreetingTriggered, 1, 0) != 0)
        {
            Log.Debug("[AiAssistant] Opening greeting already triggered, CallSid: {CallSid}", context.CallSid);
            return;
        }

        await actions.SendTextToProviderAsync($"Greet the user with: '{greeting}'").ConfigureAwait(false);
    }

    private Task HandleClientStartAsync(string sessionId, Dictionary<string, string> metadata)
    {
        metadata.TryGetValue("callSid", out var callSid);
        metadata.TryGetValue("streamSid", out var streamSid);

        _ctx.CallSid = callSid;
        _ctx.StreamSid = streamSid;

        Log.Information("[AiAssistant] Call started, CallSid: {CallSid}, StreamSid: {StreamSid}", callSid, streamSid);

        TriggerTwilioRecordingPhoneCall();

        if (!_ctx.IsInAiServiceHours && _ctx.IsEnableManualService) TransferHumanService(_ctx.TransferCallNumber);

        return Task.CompletedTask;
    }

    private Task HandleClientStopAsync(string sessionId)
    {
        Log.Information("[AiAssistant] Twilio stop event received, SessionId: {SessionId}, CallSid: {CallSid}, StreamSid: {StreamSid}", sessionId, _ctx.CallSid, _ctx.StreamSid);

        return Task.CompletedTask;
    }

    private Task HandleSessionEndedAsync(string sessionId)
    {
        Log.Information("[AiAssistant] Session ended, SessionId: {SessionId}, CallSid: {CallSid}", sessionId, _ctx.CallSid);

        return Task.CompletedTask;
    }

    private Task HandleTranscriptionsCompletedAsync(
        string sessionId, IReadOnlyList<(AiSpeechAssistantSpeaker Speaker, string Text)> transcriptions)
    {
        var streamContext = new AiSpeechAssistantStreamContextDto
        {
            CallSid = _ctx.CallSid,
            StreamSid = _ctx.StreamSid,
            Host = _ctx.Host,
            Assistant = _ctx.Assistant,
            Knowledge = _ctx.Knowledge,
            LastPrompt = _ctx.Prompt,
            OrderItems = _ctx.OrderItems,
            ComplaintInfo = _ctx.ComplaintInfo,
            UserInfo = _ctx.UserInfo,
            LastUserInfo = _ctx.LastUserInfo,
            IsTransfer = _ctx.IsTransfer,
            HumanContactPhone = _ctx.HumanContactPhone,
            TransferCallNumber = _ctx.TransferCallNumber,
            IsInAiServiceHours = _ctx.IsInAiServiceHours,
            IsEnableManualService = _ctx.IsEnableManualService,
            ConversationTranscription = transcriptions.Select(t => (t.Speaker, t.Text)).ToList()
        };

        GenerateRecordFromCall(streamContext);

        return Task.CompletedTask;
    }

    private Task HandleRecordingCompleteAsync(string sessionId, byte[] wavBytes)
    {
        Log.Information("[AiAssistant] Recording complete, SessionId: {SessionId}, Size: {Size}bytes", sessionId, wavBytes.Length);

        return Task.CompletedTask;
    }
}
