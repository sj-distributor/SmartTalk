using System.Text.Json;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.Default;

public class DefaultRealtimeAiClientAdapter : IRealtimeAiClientAdapter
{
    public RealtimeAiClient Client => RealtimeAiClient.Default;

    public (RealtimeAiClientMessageType Type, string Payload) ParseMessage(string rawMessage)
    {
        using var doc = JsonDocument.Parse(rawMessage);
        var root = doc.RootElement;

        if (root.TryGetProperty("media", out var media))
            return ParseMediaPayload(media);

        if (root.TryGetProperty("text", out var textProp) && !string.IsNullOrWhiteSpace(textProp.GetString()))
            return (RealtimeAiClientMessageType.Text, textProp.GetString());

        return (RealtimeAiClientMessageType.Unknown, null);
    }

    private static (RealtimeAiClientMessageType Type, string Payload) ParseMediaPayload(JsonElement media)
    {
        if (!media.TryGetProperty("payload", out var p))
            return (RealtimeAiClientMessageType.Unknown, null);

        var payload = p.GetString();

        if (string.IsNullOrWhiteSpace(payload))
            return (RealtimeAiClientMessageType.Unknown, null);

        // Client sends media.type = "video" for camera frames (JPEG),
        // treated as image; all other types (including "audio" and absent) are audio.
        var mediaType = media.TryGetProperty("type", out var t) ? t.GetString() : null;

        return mediaType switch
        {
            "video" => (RealtimeAiClientMessageType.Image, payload),
            _ => (RealtimeAiClientMessageType.Audio, payload)
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

    public object BuildTranscriptionMessage(RealtimeAiWssEventType eventType, RealtimeAiWssTranscriptionData transcriptionData, string sessionId)
    {
        return new { type = eventType.ToString(), Data = new { transcriptionData }, session_id = sessionId };
    }

    public object BuildErrorMessage(string code, string message, string sessionId)
    {
        return new { type = "ClientError", session_id = sessionId, Data = new { Code = code, Message = message } };
    }
}
