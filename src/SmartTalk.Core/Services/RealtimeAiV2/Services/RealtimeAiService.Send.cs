using System.Net.WebSockets;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
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

    // ── High-level ────────────────────────────────────────────────

    private async Task SendAudioToClientAsync(string base64Payload)
    {
        await SendToClientAsync(_ctx.ClientAdapter.BuildAudioDeltaMessage(base64Payload, _ctx.SessionId)).ConfigureAwait(false);
    }

    private async Task SendAudioToProviderAsync(string base64Payload)
    {
        await SendToProviderAsync(_ctx.ProviderAdapter.BuildAudioAppendMessage(new RealtimeAiWssAudioData { Base64Payload = base64Payload })).ConfigureAwait(false);
    }

    private async Task SendImageToProviderAsync(string base64Payload)
    {
        await SendToProviderAsync(_ctx.ProviderAdapter.BuildAudioAppendMessage(new RealtimeAiWssAudioData
        {
            Base64Payload = base64Payload,
            CustomProperties = new Dictionary<string, object> { { "image", base64Payload } }
        })).ConfigureAwait(false);
    }

    private async Task SendTextToProviderAsync(string text)
    {
        Log.Information("[RealtimeAi] Sending text to provider, SessionId: {SessionId}, Text: {Text}", _ctx.SessionId, text);

        await SendToProviderAsync(_ctx.ProviderAdapter.BuildTextUserMessage(text, _ctx.SessionId)).ConfigureAwait(false);
        await QueueOrTriggerProviderResponseAsync("text input").ConfigureAwait(false);
    }

    private async Task QueueOrTriggerProviderResponseAsync(string source)
    {
        if (!IsProviderSessionActive) return;

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var shouldSendTrigger = false;

        await _ctx.ProviderResponseStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_ctx.IsProviderResponseInProgress)
            {
                _ctx.HasPendingProviderResponseTrigger = true;
                Log.Information("[RealtimeAi] Response trigger queued, SessionId: {SessionId}, Source: {Source}", _ctx.SessionId, source);
                return;
            }

            _ctx.IsProviderResponseInProgress = true;
            shouldSendTrigger = true;
        }
        finally
        {
            _ctx.ProviderResponseStateLock.Release();
        }

        if (!shouldSendTrigger) return;

        try
        {
            await SendToProviderAsync(_ctx.ProviderAdapter.BuildTriggerResponseMessage()).ConfigureAwait(false);
        }
        catch
        {
            await _ctx.ProviderResponseStateLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                _ctx.IsProviderResponseInProgress = false;
                _ctx.HasPendingProviderResponseTrigger = true;
            }
            finally
            {
                _ctx.ProviderResponseStateLock.Release();
            }

            throw;
        }
    }

    private async Task MarkProviderResponseStartedAsync()
    {
        if (!IsProviderSessionActive) return;

        await _ctx.ProviderResponseStateLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            _ctx.IsProviderResponseInProgress = true;
        }
        finally
        {
            _ctx.ProviderResponseStateLock.Release();
        }
    }

    private async Task MarkProviderResponseCompletedAndDrainAsync()
    {
        if (!IsProviderSessionActive) return;

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var shouldSendQueuedTrigger = false;

        await _ctx.ProviderResponseStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _ctx.IsProviderResponseInProgress = false;

            if (_ctx.HasPendingProviderResponseTrigger)
            {
                _ctx.HasPendingProviderResponseTrigger = false;
                _ctx.IsProviderResponseInProgress = true;
                shouldSendQueuedTrigger = true;
            }
        }
        finally
        {
            _ctx.ProviderResponseStateLock.Release();
        }

        if (shouldSendQueuedTrigger)
            await SendToProviderAsync(_ctx.ProviderAdapter.BuildTriggerResponseMessage()).ConfigureAwait(false);
    }

    private async Task SendSessionUpdateAsync(RealtimeAiSessionUpdate update)
    {
        if (update == null) return;

        var message = _ctx.ProviderAdapter.BuildSessionUpdateMessage(update);

        if (string.IsNullOrWhiteSpace(message)) return;

        await SendToProviderAsync(message).ConfigureAwait(false);

        if (update.Instructions != null)
            _ctx.Options.ModelConfig.Prompt = update.Instructions;

        if (update.Tools != null)
            _ctx.Options.ModelConfig.Tools = update.Tools;
    }
}
