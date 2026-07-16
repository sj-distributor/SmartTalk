using System.Text.Json;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.RealtimeHttpGateway;

public class RealtimeHttpGatewayRealtimeAiClientAdapter : IRealtimeAiClientAdapter
{
    public RealtimeAiClient Client => RealtimeAiClient.RealtimeHttpGateway;

    public RealtimeAiAudioCodec NativeAudioCodec => RealtimeAiAudioCodec.PCM16;

    public ParsedClientMessage ParseMessage(string rawMessage)
    {
        using var doc = JsonDocument.Parse(rawMessage);
        var root = doc.RootElement;

        if (!TryGetString(root, "type", out var type))
            return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Unknown };

        return type switch
        {
            "RealtimeHttpTextInput" when TryGetString(root, "text", out var text) =>
                new ParsedClientMessage { Type = RealtimeAiClientMessageType.Text, Payload = text },
            "RealtimeHttpRecordingAudio" when TryGetString(root, "payload", out var payload) =>
                new ParsedClientMessage { Type = RealtimeAiClientMessageType.RecordingAudio, Payload = payload },
            _ => new ParsedClientMessage { Type = RealtimeAiClientMessageType.Unknown }
        };
    }

    public object BuildAudioDeltaMessage(string base64Payload, string sessionId)
    {
        return new { type = "ResponseAudioDelta", Data = new { Base64Payload = base64Payload }, session_id = sessionId };
    }

    public object BuildSpeechDetectedMessage(string sessionId)
    {
        return new { type = "SpeechDetected", session_id = sessionId };
    }

    public object BuildTurnCompletedMessage(string sessionId)
    {
        return new { type = "AiTurnCompleted", session_id = sessionId };
    }

    public object BuildTranscriptionMessage(
        RealtimeAiWssEventType eventType,
        RealtimeAiWssTranscriptionData transcriptionData,
        string sessionId)
    {
        return new { type = eventType.ToString(), Data = new { transcriptionData }, session_id = sessionId };
    }

    public object BuildErrorMessage(string code, string message, string sessionId)
    {
        return new { type = "ClientError", session_id = sessionId, Data = new { Code = code, Message = message } };
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
            return false;

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
