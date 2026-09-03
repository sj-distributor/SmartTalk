namespace SmartTalk.Api.Authentication.TemporarySession;

public static class TemporarySessionAuthenticationDefaults
{
    public const string HeaderName = "X-INTERVIEW-SESSION";
    public const string WebSocketProtocolPrefix = HeaderName + ".";
    public const string QueryParameterName = "sessionId";

    public const string AccountOrTemporarySessionPolicy = "AccountOrTemporarySession";
    public const string TemporarySessionPolicy = "TemporarySessionOnly";

    public const string CredentialTypeClaim = "credential_type";
    public const string CredentialType = "temporary_session";
    public const string SessionIdClaim = "session_id";
    public const string AssistantIdClaim = "assistant_id";
}
