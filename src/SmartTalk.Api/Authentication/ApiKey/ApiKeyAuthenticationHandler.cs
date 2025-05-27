using System.Security.Claims;
using SmartTalk.Core.Constants;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Messages.Enums.Caching;
using Microsoft.AspNetCore.Authentication;

namespace SmartTalk.Api.Authentication.ApiKey;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly ICacheManager _cacheManager;
    private readonly IAccountDataProvider _accountDataProvider;
    
    public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, ISystemClock clock, ICacheManager cacheManager, IAccountDataProvider accountDataProvider) : base(options, logger, encoder, clock)
    {
        _cacheManager = cacheManager;
        _accountDataProvider = accountDataProvider;
    }
    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-API-KEY") || (Context.WebSockets.IsWebSocketRequest && Context.WebSockets.WebSocketRequestedProtocols.Contains("X-API-KEY")))
            return AuthenticateResult.NoResult();
        
        var apiKey = Context.WebSockets.IsWebSocketRequest
            ? Context.WebSockets.WebSocketRequestedProtocols.FirstOrDefault(x => x.StartsWith("X-API-KEY"))
            : Context.Request.Headers["X-API-KEY"].ToString();
        
        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.NoResult();

        var userInfo = await _cacheManager.GetOrAddAsync(apiKey,
            async _ => await _accountDataProvider.GetUserAccountByApiKeyAsync(apiKey).ConfigureAwait(false),
            new RedisCachingSetting(expiry: TimeSpan.FromHours(24)), CancellationToken.None);
        
        if (userInfo == null)
            return AuthenticateResult.NoResult();

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, userInfo.UserName),
            new Claim(ClaimTypes.NameIdentifier, userInfo.Id.ToString()),
        }, AuthenticationSchemeConstants.ApiKeyAuthenticationScheme);
        
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var authenticationTicket = new AuthenticationTicket(claimsPrincipal,
            new AuthenticationProperties { IsPersistent = false }, Scheme.Name);
            
        Request.HttpContext.User = claimsPrincipal;

        return AuthenticateResult.Success(authenticationTicket);
    }
}