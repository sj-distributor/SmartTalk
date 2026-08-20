using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Constants;

namespace SmartTalk.Api.Authentication.TemporarySession;

public sealed class TemporarySessionAuthenticationHandler : AuthenticationHandler<TemporarySessionAuthenticationOptions>
{
    private const string InvalidSessionMessage = "The interview session is invalid or has expired.";
    private const string ChallengeWrittenItem = "TemporarySessionChallengeWritten";

    private readonly IAiSpeechAssistantSessionCredentialService _credentialService;

    public TemporarySessionAuthenticationHandler(
        IOptionsMonitor<TemporarySessionAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IAiSpeechAssistantSessionCredentialService credentialService)
        : base(options, logger, encoder, clock)
    {
        _credentialService = credentialService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var rawSessionId = GetSessionId();
        if (string.IsNullOrWhiteSpace(rawSessionId)) return AuthenticateResult.NoResult();

        if (HasAccountCredential())
            return AuthenticateResult.Fail("Temporary session credentials cannot be combined with account credentials.");

        if (!Guid.TryParse(rawSessionId, out var sessionId))
            return AuthenticateResult.Fail(InvalidSessionMessage);

        var credential = await _credentialService
            .GetValidAsync(sessionId, Context.RequestAborted)
            .ConfigureAwait(false);

        if (credential == null || !MatchesAssistantRoute(credential.AssistantId))
            return AuthenticateResult.Fail(InvalidSessionMessage);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "internal_user"),
            new Claim(ClaimTypes.NameIdentifier, CurrentUsers.InternalUser.Id.ToString()),
            new Claim(ClaimTypes.Authentication, AuthenticationSchemeConstants.SelfAuthenticationScheme),
            new Claim(TemporarySessionAuthenticationDefaults.CredentialTypeClaim, TemporarySessionAuthenticationDefaults.CredentialType),
            new Claim(TemporarySessionAuthenticationDefaults.SessionIdClaim, credential.SessionId.ToString("D")),
            new Claim(TemporarySessionAuthenticationDefaults.AssistantIdClaim, credential.AssistantId.ToString())
        }, Scheme.Name);

        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Context.Items.ContainsKey(ChallengeWrittenItem)) return;
        Context.Items[ChallengeWrittenItem] = true;

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            code = StatusCodes.Status401Unauthorized,
            msg = InvalidSessionMessage
        });
        await Response.WriteAsync(payload, Context.RequestAborted).ConfigureAwait(false);
    }

    private string? GetSessionId()
    {
        var headerValue = Request.Headers[TemporarySessionAuthenticationDefaults.HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerValue)) return headerValue;

        if (!IsWebSocketUpgradeRequest()) return null;

        var protocol = GetRequestedWebSocketProtocols().FirstOrDefault(x =>
            x.StartsWith(TemporarySessionAuthenticationDefaults.WebSocketProtocolPrefix, StringComparison.OrdinalIgnoreCase));
        if (protocol != null)
            return protocol[TemporarySessionAuthenticationDefaults.WebSocketProtocolPrefix.Length..];

        return Request.Query[TemporarySessionAuthenticationDefaults.QueryParameterName].FirstOrDefault();
    }

    private bool HasAccountCredential()
    {
        if (!string.IsNullOrWhiteSpace(Request.Headers.Authorization)) return true;
        if (!string.IsNullOrWhiteSpace(Request.Headers["X-API-KEY"])) return true;

        return IsWebSocketUpgradeRequest() &&
               GetRequestedWebSocketProtocols().Any(x =>
                   x.StartsWith("X-API-KEY.", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsWebSocketUpgradeRequest()
    {
        return HttpMethods.IsGet(Request.Method) &&
               Request.Headers["Connection"].ToString().Split(',', StringSplitOptions.TrimEntries)
                   .Any(x => x.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) &&
               Request.Headers["Upgrade"].ToString().Equals("websocket", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> GetRequestedWebSocketProtocols()
    {
        return Request.Headers["Sec-WebSocket-Protocol"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private bool MatchesAssistantRoute(int assistantId)
    {
        if (!Request.RouteValues.TryGetValue("assistantId", out var routeValue)) return true;

        return int.TryParse(routeValue?.ToString(), out var routeAssistantId) && routeAssistantId == assistantId;
    }
}
