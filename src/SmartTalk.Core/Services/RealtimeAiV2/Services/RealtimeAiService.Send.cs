using System.Net.WebSockets;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    // ── Low-level ───────────────────────────────────────────────

    private async Task SendToClientAsync(object payload)
    {
        if (_ctx.WebSocket is not { State: WebSocketState.Open }) return;

        await _ctx.WsSendLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

            await _ctx.WebSocket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, _ctx.SessionCts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "[RealtimeAi] Failed to send to client, SessionId: {SessionId}, WebSocketState: {WebSocketState}", _ctx.SessionId, _ctx.WebSocket?.State);
        }
        finally
        {
            _ctx.WsSendLock.Release();
        }
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

    // ── High-level: audio with codec conversion ─────────────────

    private async Task SendAudioToProviderAsync(RealtimeAiWssAudioData audioData)
    {
        var raw = Convert.FromBase64String(audioData.Base64Payload);
        var providerCodec = _ctx.ProviderAdapter.GetPreferredCodec(_ctx.ClientAdapter.NativeAudioCodec);
        var converted = AudioCodecConverter.Convert(raw, _ctx.ClientAdapter.NativeAudioCodec, providerCodec);
        
        audioData.Base64Payload = Convert.ToBase64String(converted);

        await SendToProviderAsync(_ctx.ProviderAdapter.BuildAudioAppendMessage(audioData)).ConfigureAwait(false);
    }

    private async Task SendProviderAudioToClientAsync(string providerBase64)
    {
        var raw = Convert.FromBase64String(providerBase64);
        var providerCodec = _ctx.ProviderAdapter.GetPreferredCodec(_ctx.ClientAdapter.NativeAudioCodec);
        var clientBytes = AudioCodecConverter.Convert(raw, providerCodec, _ctx.ClientAdapter.NativeAudioCodec);

        await SendToClientAsync(_ctx.ClientAdapter.BuildAudioDeltaMessage(Convert.ToBase64String(clientBytes), _ctx.SessionId)).ConfigureAwait(false);
    }

    // ── High-level: no codec conversion ─────────────────────────

    private async Task SendAudioToClientAsync(string base64Payload)
    {
        await SendToClientAsync(_ctx.ClientAdapter.BuildAudioDeltaMessage(base64Payload, _ctx.SessionId)).ConfigureAwait(false);
    }

    private async Task SendTextToProviderAsync(string text)
    {
        Log.Information("[RealtimeAi] Sending text to provider, SessionId: {SessionId}, Text: {Text}", _ctx.SessionId, text);

        await SendToProviderAsync(
            _ctx.ProviderAdapter.BuildTextUserMessage(text, _ctx.SessionId),
            _ctx.ProviderAdapter.BuildTriggerResponseMessage()
        ).ConfigureAwait(false);
    }
}
