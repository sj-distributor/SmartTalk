using System.Net.WebSockets;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private async Task ConnectToProviderAsync()
    {
        SubscribeProviderEvents();
        SubscribeTtsEvents();

        var serviceUri = new Uri(_ctx.Options.ModelConfig.ServiceUrl);
        var headers = _ctx.ProviderAdapter.GetHeaders(_ctx.Options.Region);

        var ttsConfig = _ctx.Options.TtsConfig ?? new RealtimeAiTtsConfig();
        if (_ctx.TtsProvider.TtsProviderType == RealtimeAiTtsProviderType.BuiltIn)
        {
            ttsConfig.TargetCodec = _ctx.ProviderAdapter.GetPreferredCodec(_ctx.ClientAdapter.NativeAudioCodec);
        }

        await _ctx.TtsProvider.InitializeAsync(ttsConfig, _ctx.SessionCts.Token).ConfigureAwait(false);

        if (_ctx.WssClient.CurrentState != WebSocketState.Open || _ctx.WssClient.EndpointUri != serviceUri)
            await _ctx.WssClient.ConnectAsync(serviceUri, headers, _ctx.SessionCts.Token).ConfigureAwait(false);

        if (_ctx.WssClient.CurrentState != WebSocketState.Open)
            throw new InvalidOperationException("Failed to connect to AI provider WebSocket.");

        var sessionConfig = _ctx.ProviderAdapter.BuildSessionConfig(_ctx.Options, _ctx.ClientAdapter.NativeAudioCodec);
        var configJson = JsonConvert.SerializeObject(sessionConfig, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        await _ctx.WssClient.SendMessageAsync(configJson, _ctx.SessionCts.Token).ConfigureAwait(false);

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
        UnsubscribeTtsEvents();

        if (_ctx.TtsProvider != null)
            await _ctx.TtsProvider.StopAsync(CancellationToken.None).ConfigureAwait(false);

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

    private void SubscribeTtsEvents()
    {
        _ctx.TtsProvider.AudioChunkReadyAsync += OnTtsAudioChunkReadyAsync;
        _ctx.TtsProvider.SynthesisCompletedAsync += OnTtsSynthesisCompletedAsync;
        _ctx.TtsProvider.SynthesisFailedAsync += OnTtsSynthesisFailedAsync;
    }

    private void UnsubscribeTtsEvents()
    {
        if (_ctx.TtsProvider == null) return;

        _ctx.TtsProvider.AudioChunkReadyAsync -= OnTtsAudioChunkReadyAsync;
        _ctx.TtsProvider.SynthesisCompletedAsync -= OnTtsSynthesisCompletedAsync;
        _ctx.TtsProvider.SynthesisFailedAsync -= OnTtsSynthesisFailedAsync;
    }
}
