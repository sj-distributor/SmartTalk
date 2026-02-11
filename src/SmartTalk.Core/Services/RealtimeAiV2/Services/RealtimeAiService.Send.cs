using Serilog;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private async Task SendTextToProviderAsync(string text)
    {
        Log.Information("[RealtimeAi] Sending text to provider, SessionId: {SessionId}, Text: {Text}", _ctx.SessionId, text);

        await SendToProviderAsync(
            _ctx.Adapter.BuildTextUserMessage(text, _ctx.SessionId),
            _ctx.Adapter.BuildTriggerResponseMessage()
        ).ConfigureAwait(false);
    }

    private async Task SendAudioToProviderAsync(RealtimeAiWssAudioData audioData)
    {
        await SendToProviderAsync(_ctx.Adapter.BuildAudioAppendMessage(audioData)).ConfigureAwait(false);
    }

    private async Task SendToProviderAsync(params string[] messages)
    {
        if (!IsProviderSessionActive) return;

        foreach (var message in messages)
        {
            if (message != null)
                await _ctx.WssClient.SendMessageAsync(message, _ctx.SessionCts.Token).ConfigureAwait(false);
        }
    }
}
