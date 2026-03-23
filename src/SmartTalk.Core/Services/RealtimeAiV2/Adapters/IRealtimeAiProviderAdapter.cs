using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAiV2;
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

    object BuildInterruptMessage(string lastAssistantItemIdToInterrupt);

    string BuildFunctionCallReplyMessage(RealtimeAiWssFunctionCallData functionCall, string output);

    /// <summary>
    /// Builds a provider-specific session update message (e.g., OpenAI "session.update").
    /// Returns null if the provider does not support session updates.
    /// </summary>
    string BuildSessionUpdateMessage(RealtimeAiSessionUpdate update);

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