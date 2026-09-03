using System.Diagnostics;
using System.Net.WebSockets;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Core.Services.RealtimeAiV2.Negotiation;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    /// <summary>
    /// Whether the voice provider is driven by text, read from which capability sibling it implements
    /// rather than from its vendor label.
    ///
    /// <para>This used to be <c>TtsProviderType != BuiltIn</c>, which made the engine the place a new
    /// vendor had to be taught about — and got a provider that passes audio through driven in text
    /// mode purely for not being the built-in one, which is a silently mute call. The two vendors that
    /// exist map identically either way; the difference is that a third does not need this file.</para>
    ///
    /// <para>The question is whether the provider REQUIRES text, so a provider implementing both
    /// siblings answers no: it can be handed audio, and which of the two it is actually driven by is
    /// then <c>OutputMode</c>'s decision, resolved from the inference side. Declaring neither is a
    /// fault rather than a default — such a provider can be handed nothing it can use — and throws the
    /// same exception the negotiator throws for a pairing that cannot work.</para>
    /// </summary>
    private bool ResolveTtsRequiresTextInput()
    {
        if (_ctx.TtsProvider is IRealtimeAiAudioPassthrough) return false;

        if (_ctx.TtsProvider is IRealtimeAiTextSynthesizer) return true;

        throw new RealtimeAiOutputModeException($"TTS provider {_ctx.TtsProvider.GetType().Name} declares neither text synthesis nor audio passthrough, so it can be driven in no output mode.");
    }

    private async Task ConnectToProviderAsync()
    {
        SubscribeProviderEvents();
        SubscribeTtsEvents();

        var serviceUri = new Uri(_ctx.Options.ModelConfig.ServiceUrl);
        var headers = _ctx.ProviderAdapter.GetHeaders(_ctx.Options.Region);

        var ttsRequiresTextInput = ResolveTtsRequiresTextInput();

        // Decide the output mode once from the inference provider's declared capabilities and whether
        // the TTS provider needs text. Throws on an incompatible pairing (fail-loud, never silent mute).
        _ctx.OutputMode = OutputModeNegotiator.Resolve(_ctx.ProviderAdapter.Capabilities, ttsRequiresTextInput);

        var ttsConfig = _ctx.Options.TtsConfig ?? new RealtimeAiTtsConfig();

        // Only a provider that passes audio through needs the codec negotiated: a synthesizer produces
        // its own output format and declares it via OutputCodec.
        if (!ttsRequiresTextInput) ttsConfig.TargetCodec = _ctx.ProviderAdapter.GetPreferredCodec(_ctx.ClientAdapter.NativeAudioCodec);

        var ttsInitStartedAt = Stopwatch.GetTimestamp();

        await _ctx.TtsProvider.InitializeAsync(ttsConfig, _ctx.SessionCts.Token).ConfigureAwait(false);

        var ttsInitMs = (long)Stopwatch.GetElapsedTime(ttsInitStartedAt).TotalMilliseconds;
        var handshakeStartedAt = Stopwatch.GetTimestamp();

        if (_ctx.WssClient.CurrentState != WebSocketState.Open || _ctx.WssClient.EndpointUri != serviceUri)
            await _ctx.WssClient.ConnectAsync(serviceUri, headers, _ctx.SessionCts.Token).ConfigureAwait(false);

        if (_ctx.WssClient.CurrentState != WebSocketState.Open)
            throw new InvalidOperationException("Failed to connect to AI provider WebSocket.");

        var sessionConfig = _ctx.ProviderAdapter.BuildSessionConfig(_ctx.Options, _ctx.OutputMode, _ctx.ClientAdapter.NativeAudioCodec);
        var configJson = JsonConvert.SerializeObject(sessionConfig, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        await _ctx.WssClient.SendMessageAsync(configJson, _ctx.SessionCts.Token).ConfigureAwait(false);

        // OutputMode belongs here rather than on the session-start line: it is only known once the
        // negotiator has run, and reporting it before that made every call look like Audio.
        _ctx.ProviderConnectedAt = Stopwatch.GetTimestamp();

        StartProviderLivenessObserver();

        // Split so a slow connect points at a layer rather than just being slow.
        Log.Information(
            "[RealtimeAi] Connected to provider, SessionId: {SessionId}, Provider: {Provider}, OutputMode: {OutputMode}, TtsProvider: {TtsProvider}, " +
            "ElapsedConnectMs: {ElapsedConnectMs}, ElapsedTtsInitMs: {ElapsedTtsInitMs}, ElapsedProviderHandshakeMs: {ElapsedProviderHandshakeMs}",
            _ctx.SessionId, _ctx.Options.ModelConfig.Provider, _ctx.OutputMode, _ctx.TtsProvider.TtsProviderType,
            (long)Stopwatch.GetElapsedTime(_ctx.SessionStartedAt).TotalMilliseconds, ttsInitMs,
            (long)Stopwatch.GetElapsedTime(handshakeStartedAt).TotalMilliseconds);
    }

    private async Task DisconnectFromProviderAsync(string reason)
    {
        // Claimed atomically rather than inferred from SessionCts being null: both callers pass a
        // null check, and the loser then throws on the Dispose the winner already did. Exactly one
        // caller performs the teardown; the other returns, as it did before minus the exception.
        if (Interlocked.Exchange(ref _ctx.ProviderDisconnectClaimed, 1) == 1)
        {
            Log.Debug("[RealtimeAi] Provider teardown already in progress, SessionId: {SessionId}", _ctx.SessionId);
            return;
        }

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
