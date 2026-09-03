using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task HandleSessionReadyAsync(RealtimeAiSessionActions actions)
    {
        // 代客致电: 有本通 instruction 则以它驱动开场, 取代 DB 问候 —— 否则会讲到默认 assistant 的人设/问候
        // (例如默认 AIXV Link demo assistant 会自报"我是智能电话服务")。instruction 已在 prompt 覆盖 DB prompt, 这里再以一条
        // 开场指令显式触发首句, 保证 AI 一开口就按"代顾客来电"措辞。
        if (!string.IsNullOrWhiteSpace(_ctx.Instruction))
        {
            await actions.SendTextToProviderAsync(_ctx.Instruction).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrEmpty(_ctx.Knowledge?.Greetings)) return;

        await actions.SendTextToProviderAsync($"Greet the user with: '{_ctx.Knowledge.Greetings}'").ConfigureAwait(false);
    }

    private Task HandleClientStartAsync(string sessionId, Dictionary<string, string> metadata)
    {
        metadata.TryGetValue("callSid", out var callSid);
        metadata.TryGetValue("streamSid", out var streamSid);

        _ctx.CallSid = callSid;
        _ctx.StreamSid = streamSid;

        // 代客致电: instruction/question 经 <Stream><Parameter> 到达 start 帧的 customParameters; URL/path 通道不可靠时这里兜底回填。
        // question 的 URL/path 通道用于建立 session 前的 Prompt 拼接；start 帧回填仅用于后续会话上下文，不能追溯修改已发送的 session 配置。
        if (string.IsNullOrWhiteSpace(_ctx.Instruction) && metadata.TryGetValue("instruction", out var instruction) && !string.IsNullOrWhiteSpace(instruction))
            _ctx.Instruction = instruction;

        if (string.IsNullOrWhiteSpace(_ctx.Question) && metadata.TryGetValue("question", out var question) && !string.IsNullOrWhiteSpace(question))
            _ctx.Question = question;

        Log.Information("[AiAssistant] Call started, CallSid: {CallSid}, StreamSid: {StreamSid}, HasInstruction: {HasInstruction}, HasQuestion: {HasQuestion}",
            callSid, streamSid, !string.IsNullOrWhiteSpace(_ctx.Instruction), !string.IsNullOrWhiteSpace(_ctx.Question));

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
