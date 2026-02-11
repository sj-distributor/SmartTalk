using System.Net.WebSockets;
using Newtonsoft.Json;
using Serilog;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private bool IsProviderSessionActive => _ctx.SessionCts is { IsCancellationRequested: false };

    private async Task ConnectToProviderAsync()
    {
        SubscribeProviderEvents();

        var serviceUri = new Uri(_ctx.Options.ModelConfig.ServiceUrl);
        var headers = _ctx.Adapter.GetHeaders(_ctx.Options.Region);

        if (_ctx.WssClient.CurrentState != WebSocketState.Open || _ctx.WssClient.EndpointUri != serviceUri)
            await _ctx.WssClient.ConnectAsync(serviceUri, headers, _ctx.SessionCts.Token).ConfigureAwait(false);

        if (_ctx.WssClient.CurrentState != WebSocketState.Open)
            throw new InvalidOperationException("Failed to connect to AI provider WebSocket.");

        var initialPayload = await _ctx.Adapter.GetInitialSessionPayloadAsync(_ctx.Options, _ctx.SessionId, _ctx.SessionCts.Token).ConfigureAwait(false);
        var initialJson = JsonConvert.SerializeObject(initialPayload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        await _ctx.WssClient.SendMessageAsync(initialJson, _ctx.SessionCts.Token).ConfigureAwait(false);

        Log.Information("[RealtimeAi] Connected to provider, SessionId: {SessionId}, Provider: {Provider}", _ctx.SessionId, _ctx.Options.ModelConfig.Provider);
    }

    private async Task DisconnectFromProviderAsync(string reason)
    {
        if (_ctx.SessionCts == null)
        {
            Log.Debug("[RealtimeAi] Already disconnected, SessionId: {SessionId}", _ctx.SessionId);
            return;
        }

        if (!_ctx.SessionCts.IsCancellationRequested)
            await _ctx.SessionCts.CancelAsync().ConfigureAwait(false);

        UnsubscribeProviderEvents();

        if (_ctx.WssClient is { CurrentState: WebSocketState.Open })
            await _ctx.WssClient.DisconnectAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None).ConfigureAwait(false);

        _ctx.SessionCts.Dispose();
        _ctx.SessionCts = null;

        Log.Information("[RealtimeAi] Disconnected from provider, SessionId: {SessionId}, Reason: {Reason}", _ctx.SessionId, reason);
    }

    private void SubscribeProviderEvents()
    {
        _ctx.WssClient.MessageReceivedAsync += OnWssMessageReceivedAsync;
        _ctx.WssClient.StateChangedAsync += OnWssStateChangedAsync;
        _ctx.WssClient.ErrorOccurredAsync += OnWssErrorOccurredAsync;
    }

    private void UnsubscribeProviderEvents()
    {
        if (_ctx.WssClient == null) return;

        _ctx.WssClient.MessageReceivedAsync -= OnWssMessageReceivedAsync;
        _ctx.WssClient.StateChangedAsync -= OnWssStateChangedAsync;
        _ctx.WssClient.ErrorOccurredAsync -= OnWssErrorOccurredAsync;
    }
}
