using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public interface IRealtimeAiProviderAdapter : IScopedDependency
{
    RealtimeAiProvider Provider { get; }

    /// <summary>
    /// What this provider can do (text-output support, audio output, codecs), declared as data the
    /// engine reads to negotiate the output mode and validate codecs — instead of branching on
    /// <see cref="Provider"/>.
    /// </summary>
    RealtimeAiInferenceCapabilities Capabilities { get; }

    Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region);

    // outputMode is decided by the engine (OutputModeNegotiator). A text-capable adapter MUST honor it
    // and emit the matching modality; it MUST NOT infer the mode from options.TtsConfig.
    object BuildSessionConfig(RealtimeSessionOptions options, RealtimeAiOutputMode outputMode, RealtimeAiAudioCodec clientCodec);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    /// <summary>
    /// Builds the provider truncate message at user barge-in. Returns null when the
    /// provider has no equivalent; the caller skips sending on null.
    /// </summary>
    string BuildTruncateMessage(string itemId, long audioEndMs);

    string BuildFunctionCallReplyMessage(RealtimeAiWssFunctionCallData functionCall, string output);

    /// <summary>
    /// Returns a JSON message to trigger an AI response after sending text,
    /// or null if the provider auto-triggers responses.
    /// </summary>
    string BuildTriggerResponseMessage();

    /// <summary>
    /// Returns the codec the provider will actually use, given the client's codec.
    /// Providers that support multiple codecs (e.g. OpenAI) return the client's codec to avoid conversion.
    /// Providers with a fixed codec (e.g. Google = PCM16) always return their own codec.
    /// </summary>
    RealtimeAiAudioCodec GetPreferredCodec(RealtimeAiAudioCodec clientCodec);

    ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage);
}