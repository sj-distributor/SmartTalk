using System.Net;

namespace SmartTalk.Core.Services.RealtimeHttp;

public class RealtimeHttpGatewayException : Exception
{
    public RealtimeHttpGatewayException(
        string code,
        string message,
        HttpStatusCode statusCode,
        string sessionId = "",
        string providerSessionId = "",
        string reason = "")
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        SessionId = sessionId;
        ProviderSessionId = providerSessionId;
        Reason = reason;
    }

    public string Code { get; }

    public HttpStatusCode StatusCode { get; }

    public string SessionId { get; }

    public string ProviderSessionId { get; }

    public string Reason { get; }

    public static RealtimeHttpGatewayException SessionNotFound(string sessionId)
    {
        return new RealtimeHttpGatewayException(
            "session_not_found",
            "Realtime HTTP session was not found.",
            HttpStatusCode.NotFound,
            sessionId);
    }

    public static RealtimeHttpGatewayException SessionClosed(
        string sessionId,
        string providerSessionId,
        string reason)
    {
        return new RealtimeHttpGatewayException(
            "session_closed",
            "Realtime HTTP session has already been closed.",
            HttpStatusCode.Gone,
            sessionId,
            providerSessionId,
            reason);
    }

    public static RealtimeHttpGatewayException SessionBusy(string sessionId)
    {
        return new RealtimeHttpGatewayException(
            "session_busy",
            "Realtime HTTP session is already processing another message.",
            HttpStatusCode.Conflict,
            sessionId);
    }

    public static RealtimeHttpGatewayException TtsUnavailable(string sessionId)
    {
        return new RealtimeHttpGatewayException(
            "tts_unavailable",
            "Text-to-speech did not produce usable PCM16 audio.",
            HttpStatusCode.BadGateway,
            sessionId);
    }

    public static RealtimeHttpGatewayException AiResponseTimeout(
        string sessionId,
        string providerSessionId,
        string lastEventType)
    {
        return new RealtimeHttpGatewayException(
            "ai_response_timeout",
            $"AI turn did not complete before timeout. Last event: {lastEventType}.",
            HttpStatusCode.GatewayTimeout,
            sessionId,
            providerSessionId,
            lastEventType);
    }

    public static RealtimeHttpGatewayException ProviderError(
        string sessionId,
        string providerSessionId,
        string message)
    {
        return new RealtimeHttpGatewayException(
            "provider_error",
            string.IsNullOrWhiteSpace(message) ? "Realtime provider returned an error." : message,
            HttpStatusCode.BadGateway,
            sessionId,
            providerSessionId,
            "provider_error");
    }
}
