using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public enum RealtimeAiClientMessageType { Audio, Image, Text, Start, Stop, Unknown }

public class ParsedClientMessage
{
    public string Payload { get; set; }
    
    public RealtimeAiClientMessageType Type { get; set; }

    /// <summary>
    /// Metadata from lifecycle events (e.g. CallSid, StreamSid from Twilio "start").
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}

public interface IRealtimeAiClientAdapter : IScopedDependency
{
    RealtimeAiClient Client { get; }

    /// <summary>
    /// The audio codec the client natively uses (e.g. Twilio = MULAW).
    /// </summary>
    RealtimeAiAudioCodec NativeAudioCodec { get; }

    // Inbound: parse raw client message into a typed message
    ParsedClientMessage ParseMessage(string rawMessage);

    // Outbound: build messages to send back to client
    object BuildAudioDeltaMessage(string base64Payload, string sessionId);
    
    object BuildSpeechDetectedMessage(string sessionId);
    
    object BuildTurnCompletedMessage(string sessionId);
    
    object BuildTranscriptionMessage(RealtimeAiWssEventType eventType, RealtimeAiWssTranscriptionData transcriptionData, string sessionId);
    
    object BuildErrorMessage(string code, string message, string sessionId);
}
