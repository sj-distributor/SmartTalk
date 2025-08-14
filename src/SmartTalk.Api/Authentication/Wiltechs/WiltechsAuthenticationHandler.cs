using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Http;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Caching;

namespace SmartTalk.Api.Authentication.Wiltechs;

public class WiltechsAuthenticationHandler : AuthenticationHandler<WiltechsAuthenticationOptions>
{
    private readonly ICacheManager _cacheManager;
    private readonly IAccountService _accountService;
    private readonly ISmartTalkHttpClientFactory _clientFactory;

    public WiltechsAuthenticationHandler(ICacheManager cacheManager, IOptionsMonitor<WiltechsAuthenticationOptions> options, 
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IAccountService accountService, ISmartTalkHttpClientFactory clientFactory)
        : base(options, logger, encoder, clock)
    {
        _cacheManager = cacheManager;
        _accountService = accountService;
        _clientFactory = clientFactory;
    }
    

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        var authorization = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer"))
            return AuthenticateResult.NoResult();

        try
        {
            var userInfo = await _cacheManager.GetOrAddAsync(authorization, 
                async () => await GetUserInfoAsync(authorization), new RedisCachingSetting(expiry: TimeSpan.FromDays(30))).ConfigureAwait(false);

            if (userInfo.UserId == Guid.Empty && string.IsNullOrWhiteSpace(userInfo.UserName))
            {
                await _cacheManager.RemoveAsync(authorization, new RedisCachingSetting()).ConfigureAwait(false);
                
                return AuthenticateResult.NoResult();
            }

            var userAccount = await _cacheManager.GetOrAddAsync(userInfo.UserId.ToString(), async () => 
                await _accountService.GetOrCreateUserAccountFromThirdPartyAsync(userInfo.UserId.ToString(), userInfo.UserName, UserAccountIssuer.Wiltechs, default)
                    .ConfigureAwait(false), new RedisCachingSetting(expiry: TimeSpan.FromDays(30))).ConfigureAwait(false);

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, userAccount.UserName),
                new Claim(ClaimTypes.NameIdentifier, userAccount.Id.ToString()),
                new Claim(ClaimTypes.Authentication, UserAccountIssuer.Wiltechs.ToString())
            }, AuthenticationSchemeConstants.WiltechsAuthenticationScheme);

            userAccount.Roles.ForEach(x => identity.AddClaim(new Claim(ClaimTypes.Role, x.Name)));
            
            var claimsPrincipal = new ClaimsPrincipal(identity);
            
            var authenticationTicket = new AuthenticationTicket(claimsPrincipal,
                new AuthenticationProperties { IsPersistent = false }, Scheme.Name);

            Request.HttpContext.User = claimsPrincipal;

            return AuthenticateResult.Success(authenticationTicket);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    private async Task<WiltechsUserInfo> GetUserInfoAsync(string authorization)
    {
        return GetUserFromJwtToken(authorization) ?? await GetUserFromAuthorizationServerAsync(authorization);
    }

    private WiltechsUserInfo? GetUserFromJwtToken(string authorization)
    {
        var token = authorization.Replace("Bearer ", "");
        
        var tokenHandler = new JwtSecurityTokenHandler();

        if (!tokenHandler.CanReadToken(token)) return null;

        foreach (var issuer in Options.Issuers)
        {
            var validateParameter = new TokenValidationParameters
            {
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SymmetricKey))
            };
            
            try
            {
                var claimsPrincipal =
                    tokenHandler.ValidateToken(token, validateParameter, out _);
            
                return new WiltechsUserInfo
                {
                    UserId = Guid.Parse(claimsPrincipal.Claims.Single(x => x.Type == "UserId").Value),
                    UserName = claimsPrincipal.Claims.First(x => x.Type == "UserName").Value
                };
            }
            catch
            {
                // ignored
            }
        }
        
        return null;
    }
    
    private async Task<WiltechsUserInfo> GetUserFromAuthorizationServerAsync(string authorization)
    {
        var headers = new Dictionary<string, string> { { "Authorization", authorization } };
        
        return await _clientFactory
            .GetAsync<WiltechsUserInfo>(Options.Authority, default, headers: headers).ConfigureAwait(false);
    }
}