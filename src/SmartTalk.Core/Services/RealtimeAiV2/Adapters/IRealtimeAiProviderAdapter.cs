using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public interface IRealtimeAiProviderAdapter : IScopedDependency
{
    RealtimeAiProvider Provider { get; }
    
    Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region);

    object BuildSessionConfig(RealtimeSessionOptions options, RealtimeAiAudioCodec clientCodec);
    
    string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData);
    
    string BuildTextUserMessage(string text, string sessionId);

    /// <summary>
    /// Builds the provider-specific message that truncates the in-flight assistant
    /// turn at <paramref name="audioEndMs"/> milliseconds, called when the user barges
    /// in. Returns <c>null</c> when the provider has no equivalent (Google's Live API
    /// relies on its server-side VAD instead of an explicit client truncate). The
    /// caller skips <c>SendToProviderAsync</c> on null without warning.
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