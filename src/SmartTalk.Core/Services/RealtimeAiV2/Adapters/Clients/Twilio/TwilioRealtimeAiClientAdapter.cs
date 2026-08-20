using System.Text.Json;
using Serilog;
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

                // Twilio <Stream><Parameter name value> → start.customParameters。代客致电的本通 instruction 走这里可靠到达
                // (URL 查询串在 Media Streams 不可靠)。原样并入 metadata, 供 OnClientStartAsync 取用。
                if (startObj.TryGetProperty("customParameters", out var customParams) && customParams.ValueKind == JsonValueKind.Object)
                {
                    foreach (var param in customParams.EnumerateObject())
                        metadata[param.Name] = param.Value.ValueKind == JsonValueKind.String ? param.Value.GetString() : param.Value.ToString();
                }

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
        var timestamp = ExtractMediaTimestamp(media);

        return mediaType switch
        {
            "video" => new ParsedClientMessage { Type = RealtimeAiClientMessageType.Image, Payload = payload, Timestamp = timestamp },
            _ => new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = payload, Timestamp = timestamp }
        };
    }

    // Twilio doc'd shape is string ms; real payloads occasionally use the numeric form.
    // Bad / missing timestamp must not drop the audio frame.
    private static long? ExtractMediaTimestamp(JsonElement media)
    {
        if (!media.TryGetProperty("timestamp", out var tsProp)) return null;

        if (tsProp.ValueKind == JsonValueKind.String && long.TryParse(tsProp.GetString(), out var parsed))
            return parsed;

        if (tsProp.ValueKind == JsonValueKind.Number && tsProp.TryGetInt64(out var direct))
            return direct;

        return null;
    }

    public object BuildAudioDeltaMessage(string base64Payload, string sessionId)
    {
        if (TryWarnMissingStreamSid(sessionId, "media")) return null;

        return new { @event = "media", streamSid = _streamSid, media = new { payload = base64Payload } };
    }

    public object BuildSpeechDetectedMessage(string sessionId)
    {
        if (TryWarnMissingStreamSid(sessionId, "clear")) return null;

        return new { @event = "clear", streamSid = _streamSid };
    }

    public object BuildTurnCompletedMessage(string sessionId)
    {
        if (TryWarnMissingStreamSid(sessionId, "mark")) return null;

        return new { @event = "mark", streamSid = _streamSid, mark = new { name = "turn_completed" } };
    }

    /// <summary>
    /// Returns true (and logs once) when the Twilio start frame hasn't arrived yet.
    /// In that race window the adapter has no valid streamSid; sending a frame with
    /// any other value would cause Twilio to silently drop it. Dropping is preferable
    /// to wasting bandwidth on a frame Twilio cannot route.
    /// </summary>
    private bool TryWarnMissingStreamSid(string sessionId, string eventName)
    {
        if (_streamSid != null) return false;

        Log.Warning(
            "[Twilio] Dropping outbound {EventName} frame: streamSid not yet set (Twilio start frame still pending). SessionId: {SessionId}",
            eventName, sessionId);

        return true;
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
