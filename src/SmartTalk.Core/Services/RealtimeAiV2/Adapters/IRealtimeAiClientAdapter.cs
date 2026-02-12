using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public enum RealtimeAiClientMessageType { Audio, Image, Text, Unknown }

public interface IRealtimeAiClientAdapter : IScopedDependency
{
    RealtimeAiClient Client { get; }
    
    // Inbound: parse raw client message into a typed message
    (RealtimeAiClientMessageType Type, string Payload) ParseMessage(string rawMessage);

    // Outbound: build messages to send back to client
    object BuildAudioDeltaMessage(string base64Payload, string sessionId);
    
    object BuildSpeechDetectedMessage(string sessionId);
    
    object BuildTurnCompletedMessage(string sessionId);
    
    object BuildTranscriptionMessage(RealtimeAiWssEventType eventType, RealtimeAiWssTranscriptionData transcriptionData, string sessionId);
    
    object BuildErrorMessage(string code, string message, string sessionId);
}
