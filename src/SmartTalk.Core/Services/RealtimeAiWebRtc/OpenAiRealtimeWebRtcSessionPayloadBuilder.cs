using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiWebRtc;

internal static class OpenAiRealtimeWebRtcSessionPayloadBuilder
{
    public static string Build(RealtimeSessionOptions options, IRealtimeAiProviderAdapter providerAdapter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ModelConfig);
        ArgumentNullException.ThrowIfNull(providerAdapter);

        if (options.ModelConfig.Provider != RealtimeAiProvider.OpenAi)
            throw new NotSupportedException("The WebRTC POC only supports the OpenAI realtime provider.");

        if (options.TtsConfig?.ProviderType != RealtimeAiTtsProviderType.BuiltIn)
            throw new NotSupportedException("The WebRTC POC only supports BuiltIn TTS.");

        var updatePayload = providerAdapter.BuildSessionConfig(
            options,
            RealtimeAiOutputMode.Audio,
            RealtimeAiAudioCodec.PCM16);

        var serialized = JsonConvert.SerializeObject(
            updatePayload,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        var envelope = JObject.Parse(serialized);
        var session = envelope["session"] as JObject
            ?? throw new InvalidOperationException("OpenAI session.update payload does not contain a session object.");

        session["model"] = ResolveModelName(options.ModelConfig);
        EnsureNativeWebRtcInterruption(session);

        return session.ToString(Formatting.None);
    }

    private static string ResolveModelName(RealtimeAiModelConfig modelConfig)
    {
        if (!string.IsNullOrWhiteSpace(modelConfig.ModelName)) return modelConfig.ModelName;

        if (Uri.TryCreate(modelConfig.ServiceUrl, UriKind.Absolute, out var serviceUri))
        {
            foreach (var part in serviceUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 && string.Equals(pair[0], "model", StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(pair[1]);
            }
        }

        return "gpt-realtime";
    }

    private static void EnsureNativeWebRtcInterruption(JObject session)
    {
        var turnDetection = session["audio"]?["input"]?["turn_detection"] as JObject;
        if (turnDetection == null) return;

        var type = turnDetection.Value<string>("type");
        if (type is not ("server_vad" or "semantic_vad")) return;

        // WebRTC/SIP sessions let OpenAI own the playback buffer. Enabling these flags makes
        // barge-in cancel the current response and automatically truncate unplayed audio.
        turnDetection["create_response"] ??= true;
        turnDetection["interrupt_response"] ??= true;
    }
}
