using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;

/// <summary>
/// One-shot (per-call) MiniMax TTS synthesis, decoupled from the live realtime session's per-turn
/// state machine in <see cref="MiniMaxRealtimeAiTtsProvider"/>. Opens its own short-lived websocket,
/// synthesizes a single piece of text, and <b>streams</b> the audio back chunk-by-chunk as MiniMax
/// produces it.
///
/// <para>
/// Used by out-of-band flows that need the MiniMax voice but are not driven by the provider's text
/// deltas — e.g. the repeat-order function call, which first turns the recorded audio into text and
/// then voices it here so the repeat matches the call's MiniMax voice. Streaming lets a long repeat
/// (a big order) start playing immediately instead of after the whole thing is synthesized.
/// </para>
/// </summary>
public interface IMiniMaxTtsSynthesizer : IScopedDependency
{
    /// <summary>
    /// Synthesizes <paramref name="text"/> with the voice/model described by <paramref name="config"/>
    /// and invokes <paramref name="onPcm16ChunkAsync"/> for each audio chunk as it arrives. Chunks are
    /// mono PCM16 little-endian at <see cref="RealtimeAiTtsConfig.SampleRate"/> (defaulting to 8 kHz),
    /// delivered in order (the callback is awaited before the next chunk is read).
    ///
    /// <para>
    /// No-op when the text is blank. Throws when synthesis fails (handshake error, <c>task_failed</c>,
    /// an idle gap, or the socket closing before <c>is_final</c>) so the caller can decide whether to
    /// fall back — note the caller may already have received earlier chunks, so a mid-stream failure
    /// should not be replayed. Returns normally only after <c>is_final</c> (a complete synthesis).
    /// </para>
    /// </summary>
    Task SynthesizeStreamingAsync(
        RealtimeAiTtsConfig config,
        string text,
        Func<byte[], Task> onPcm16ChunkAsync,
        CancellationToken cancellationToken);
}

public class MiniMaxTtsSynthesizer : IMiniMaxTtsSynthesizer
{
    private const string DefaultServiceUrl = "wss://api.minimax.io/ws/v1/t2a_v2";
    private const string DefaultModel = "speech-2.8-turbo";
    private const string DefaultVoiceId = "Chinese (Mandarin)_News_Anchor";
    private const int DefaultSampleRate = 8000;
    private const int DefaultBitrate = 128000;
    private const int HandshakeTimeoutSeconds = 10;

    // Idle (gap) timeout: the maximum wait for the NEXT message while streaming. Each received
    // message resets the window, so an arbitrarily long order keeps flowing as long as MiniMax
    // keeps producing audio — only a genuine stall (no data for this long) is treated as a hang.
    private const int IdleTimeoutSeconds = 10;

    // Absolute ceiling over the whole synthesis (connect → handshake → stream → finish), layered on
    // top of the per-message idle timeout. The idle window trips fast on a hard stall but cannot bound
    // a pathological "slow trickle that never idles out" (a chunk every <IdleTimeoutSeconds, forever);
    // this backstops that. Generous on purpose — far longer than any real order readback — so it never
    // truncates a legitimate long synthesis.
    private const int TotalSynthesisBudgetSeconds = 90;

    public async Task SynthesizeStreamingAsync(
        RealtimeAiTtsConfig config,
        string text,
        Func<byte[], Task> onPcm16ChunkAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(onPcm16ChunkAsync);

        if (string.IsNullOrWhiteSpace(text)) return;

        // Total-duration backstop: linked to the caller's token and shared by every phase below, so the
        // whole synthesis is bounded even if MiniMax trickles audio just fast enough to keep the
        // per-message idle window from ever tripping.
        using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        totalCts.CancelAfter(TimeSpan.FromSeconds(TotalSynthesisBudgetSeconds));
        var budgetToken = totalCts.Token;

        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            ws.Options.SetRequestHeader("Authorization", $"Bearer {config.ApiKey}");

        var serviceUrl = string.IsNullOrWhiteSpace(config.ServiceUrl) ? DefaultServiceUrl : config.ServiceUrl;
        var targetSampleRate = config.SampleRate ?? DefaultSampleRate;

        Log.Information("[RealtimeAi][MiniMaxTtsSynth] Connecting websocket, Url: {Url}, TargetSampleRate: {SampleRate}", serviceUrl, targetSampleRate);

        try
        {
            await ConnectWithTimeoutAsync(ws, serviceUrl, budgetToken).ConfigureAwait(false);
            await WaitForEventAsync(ws, "connected_success", budgetToken).ConfigureAwait(false);
            await SendAsync(ws, BuildTaskStartPayload(config, targetSampleRate), budgetToken).ConfigureAwait(false);
            await WaitForEventAsync(ws, "task_started", budgetToken).ConfigureAwait(false);

            await SendAsync(ws, new { @event = "task_continue", text }, budgetToken).ConfigureAwait(false);

            await StreamAudioUntilFinalAsync(ws, config, targetSampleRate, onPcm16ChunkAsync, budgetToken).ConfigureAwait(false);

            await SendTaskFinishAsync(ws, budgetToken).ConfigureAwait(false);

            Log.Information("[RealtimeAi][MiniMaxTtsSynth] Synthesis completed.");
        }
        catch (OperationCanceledException) when (totalCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Total budget tripped (as opposed to the caller cancelling): surface it as a clear failure
            // so the caller falls back (if nothing has streamed yet) or logs the truncation — the same
            // contract as the idle-timeout path, never a silent partial accepted as a complete readback.
            throw new TimeoutException($"MiniMax TTS total budget exceeded: synthesis did not finish within {TotalSynthesisBudgetSeconds}s.");
        }
        finally
        {
            await CloseAsync(ws).ConfigureAwait(false);
        }
    }

    private static async Task ConnectWithTimeoutAsync(ClientWebSocket ws, string serviceUrl, CancellationToken cancellationToken)
    {
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(HandshakeTimeoutSeconds));

        await ws.ConnectAsync(new Uri(serviceUrl), connectCts.Token).ConfigureAwait(false);
    }

    private static object BuildTaskStartPayload(RealtimeAiTtsConfig config, int sampleRate)
    {
        var model = GetStringConfig(config.ProviderSpecificConfig, "model", DefaultModel);
        var voiceId = string.IsNullOrWhiteSpace(config.Voice) ? DefaultVoiceId : config.Voice;
        var speed = GetDoubleConfig(config.ProviderSpecificConfig, "speed", 1.0d);
        var volume = GetDoubleConfig(config.ProviderSpecificConfig, "vol", 1.0d);
        var pitch = GetIntConfig(config.ProviderSpecificConfig, "pitch", 0);
        var bitrate = GetIntConfig(config.ProviderSpecificConfig, "bitrate", DefaultBitrate);

        Log.Information(
            "[RealtimeAi][MiniMaxTtsSynth] task_start, Model: {Model}, VoiceId: {VoiceId}, SampleRate: {SampleRate}",
            model, voiceId, sampleRate);

        return new
        {
            @event = "task_start",
            model,
            voice_setting = new
            {
                voice_id = voiceId,
                speed,
                vol = volume,
                pitch
            },
            audio_setting = new
            {
                sample_rate = sampleRate,
                bitrate,
                format = "pcm",
                channel = 1
            }
        };
    }

    private static async Task StreamAudioUntilFinalAsync(
        ClientWebSocket ws,
        RealtimeAiTtsConfig config,
        int targetSampleRate,
        Func<byte[], Task> onPcm16ChunkAsync,
        CancellationToken cancellationToken)
    {
        var assumedSourceSampleRate = GetIntConfig(config.ProviderSpecificConfig, "source_sample_rate", targetSampleRate);

        while (true)
        {
            var message = await ReceiveTextMessageWithIdleTimeoutAsync(ws, cancellationToken).ConfigureAwait(false);

            // is_final is MiniMax's authoritative "synthesis complete" signal. A socket close before it
            // means the stream was truncated, not finished — surface it as a failure instead of returning
            // normally, otherwise a caller that has already pushed audio would accept a half-spoken order
            // as a successful readback (and one that hasn't streamed yet would lose its chance to fall back).
            if (message == null)
                throw new InvalidOperationException("MiniMax websocket closed before is_final; synthesis is incomplete.");

            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (TryGetEvent(root, out var eventName) && eventName == "task_failed")
                throw new InvalidOperationException($"MiniMax task failed: {ExtractTaskFailedMessage(root, message)}");

            if (MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioPayload(root, out var audioBytes))
            {
                var sourceSampleRate = assumedSourceSampleRate;

                if (MiniMaxRealtimeAiTtsPayloadParser.TryExtractWavPcm16(audioBytes, out var wavSampleRate, out var wavPcm))
                {
                    audioBytes = wavPcm;
                    sourceSampleRate = wavSampleRate;
                }

                if (MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioSampleRate(root, out var audioSampleRate) && audioSampleRate > 0)
                    sourceSampleRate = audioSampleRate;

                if (sourceSampleRate != targetSampleRate)
                    audioBytes = AudioCodecConverter.Resample(audioBytes, sourceSampleRate, targetSampleRate);

                if (audioBytes.Length > 0)
                    await onPcm16ChunkAsync(audioBytes).ConfigureAwait(false);
            }

            if (root.TryGetProperty("is_final", out var finalProp) && finalProp.ValueKind == JsonValueKind.True)
                return; // Clean completion: MiniMax signalled the final chunk.
        }
    }

    /// <summary>
    /// Reads the next complete message, failing if nothing arrives within <see cref="IdleTimeoutSeconds"/>.
    /// The window is per-message, so it resets on every chunk — long syntheses are never truncated as
    /// long as audio keeps flowing; only a true stall trips it.
    /// </summary>
    private static async Task<string> ReceiveTextMessageWithIdleTimeoutAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idleCts.CancelAfter(TimeSpan.FromSeconds(IdleTimeoutSeconds));

        try
        {
            return await ReceiveTextMessageAsync(ws, idleCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"MiniMax TTS idle timeout: no audio within {IdleTimeoutSeconds}s.");
        }
    }

    private static async Task WaitForEventAsync(ClientWebSocket ws, string expectedEvent, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(HandshakeTimeoutSeconds));

        while (true)
        {
            var message = await ReceiveTextMessageAsync(ws, timeoutCts.Token).ConfigureAwait(false);
            if (message == null) throw new InvalidOperationException($"MiniMax websocket closed before '{expectedEvent}'.");

            if (TryGetEvent(message, out var eventName))
            {
                if (eventName == expectedEvent) return;
                if (eventName == "task_failed") throw new InvalidOperationException($"MiniMax task failed while waiting for '{expectedEvent}'.");
            }
        }
    }

    private static async Task SendTaskFinishAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        if (ws.State != WebSocketState.Open) return;

        try
        {
            await SendAsync(ws, new { @event = "task_finish" }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeAi][MiniMaxTtsSynth] Failed to send task_finish.");
        }
    }

    private static async Task SendAsync(ClientWebSocket ws, object payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CloseAsync(ClientWebSocket ws)
    {
        if (ws.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;

        try
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "MiniMax TTS synth done", CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort shutdown.
        }
    }

    private static async Task<string> ReceiveTextMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await ws.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close) return null;

            if (result.Count > 0)
                stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }
    }

    private static string ExtractTaskFailedMessage(JsonElement root, string fallback)
    {
        if (root.TryGetProperty("base_resp", out var baseResp) && baseResp.ValueKind == JsonValueKind.Object)
        {
            if (baseResp.TryGetProperty("status_msg", out var statusMsg) && statusMsg.ValueKind == JsonValueKind.String)
                return statusMsg.GetString();

            if (baseResp.TryGetProperty("message", out var baseMessage) && baseMessage.ValueKind == JsonValueKind.String)
                return baseMessage.GetString();
        }

        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            return message.GetString();

        return fallback;
    }

    private static bool TryGetEvent(string json, out string eventName)
    {
        eventName = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryGetEvent(doc.RootElement, out eventName);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetEvent(JsonElement root, out string eventName)
    {
        eventName = string.Empty;

        if (!root.TryGetProperty("event", out var eventProp) || eventProp.ValueKind != JsonValueKind.String)
            return false;

        eventName = eventProp.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(eventName);
    }

    private static string GetStringConfig(IDictionary<string, object> config, string key, string defaultValue)
    {
        if (config == null || !config.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString() ?? defaultValue,
            _ => defaultValue
        };
    }

    private static int GetIntConfig(IDictionary<string, object> config, string key, int defaultValue)
    {
        if (config == null || !config.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            int number => number,
            long number => (int)number,
            double number => (int)number,
            decimal number => (int)number,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var parsed) => parsed,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static double GetDoubleConfig(IDictionary<string, object> config, string key, double defaultValue)
    {
        if (config == null || !config.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            double number => number,
            float number => number,
            decimal number => (double)number,
            int number => number,
            long number => number,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDouble(out var parsed) => parsed,
            string text when double.TryParse(text, out var parsed) => parsed,
            _ => defaultValue
        };
    }
}
