using System.Text.Json;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.Twilio;

public class TwilioRealtimeAiClientAdapter : IRealtimeAiClientAdapter
{
    private string _streamSid;

    public RealtimeAiClient Client => RealtimeAiClient.Twilio;

    public RealtimeAiAudioCodec NativeAudioCodec => RealtimeAiAudioCodec.MULAW;

    public ParsedClientMessage ParseMessage(string rawMessage)
    {
        using var doc = JsonDocument.Parse(rawMessage);
        var root = doc.RootElement;

        if (root.TryGetProperty("event", out var eventProp))
        {
            var eventType = eventProp.GetString();

            if (eventType == "start" && root.TryGetProperty("start", out var startObj))
            {
                var metadata = new Dictionary<string, string>();

                if (startObj.TryGetProperty("streamSid", out var streamSidProp))
                {
                    _streamSid = streamSidProp.GetString();
                    metadata["streamSid"] = _streamSid;
                }
                if (startObj.TryGetProperty("callSid", out var callSidProp))
                    metadata["callSid"] = callSidProp.GetString();

                return new ParsedClientMessage
                {
                    Type = RealtimeAiClientMessageType.Start,
                    Metadata = metadata
                };
            }

            if (eventType == "stop")
                return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Stop };

            if (eventType == "media" && root.TryGetProperty("media", out var media))
                return ParseMediaPayload(media);
        }

        if (root.TryGetProperty("media", out var mediaFallback))
            return ParseMediaPayload(mediaFallback);

        if (root.TryGetProperty("text", out var textProp) && !string.IsNullOrWhiteSpace(textProp.GetString()))
            return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Text, Payload = textProp.GetString() };

        return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Unknown };
    }

    private static ParsedClientMessage ParseMediaPayload(JsonElement media)
    {
        if (!media.TryGetProperty("payload", out var p))
            return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Unknown };

        var payload = p.GetString();

        if (string.IsNullOrWhiteSpace(payload))
            return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Unknown };

        var mediaType = media.TryGetProperty("type", out var t) ? t.GetString() : null;

        return mediaType switch
        {
            "video" => new ParsedClientMessage { Type = RealtimeAiClientMessageType.Image, Payload = payload },
            _ => new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = payload }
        };
    }

    public object BuildAudioDeltaMessage(string base64Payload, string sessionId)
    {
        return new { @event = "media", streamSid = _streamSid ?? sessionId, media = new { payload = base64Payload } };
    }

    public object BuildSpeechDetectedMessage(string sessionId)
    {
        return new { @event = "clear", streamSid = _streamSid ?? sessionId };
    }

    public object BuildTurnCompletedMessage(string sessionId)
    {
        return new { @event = "mark", streamSid = _streamSid ?? sessionId, mark = new { name = "turn_completed" } };
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
