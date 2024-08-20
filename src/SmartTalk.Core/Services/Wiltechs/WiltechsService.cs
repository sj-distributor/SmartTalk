using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.Authentication;
using SmartTalk.Messages.Dto.Wiltechs;

namespace SmartTalk.Core.Services.Wiltechs;

public interface IWiltechsService : IScopedDependency
{
    Task<WiltechsUserInfoDto> AuthenticateAsync(string authorization, CancellationToken cancellationToken);

    Task<WiltechsUserInfoDto> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
}

public class WiltechsService : IWiltechsService
{
    private readonly IWiltechsClient _wiltechsClient;
    private readonly WiltechsAuthenticationSettings _wiltechsAuthenticationSettings;
    
    public WiltechsService(IWiltechsClient wiltechsClient, WiltechsAuthenticationSettings wiltechsAuthenticationSettings)
    {
        _wiltechsClient = wiltechsClient;
        _wiltechsAuthenticationSettings = wiltechsAuthenticationSettings;
    }

    public async Task<WiltechsUserInfoDto> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var authResponse = await _wiltechsClient
            .TokenAsync(new WiltechsTokenRequestDto { Username = username, Password = password }, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(authResponse?.AccessToken)) return null;

        return await AuthenticateAsync($"Bearer {authResponse.AccessToken}", cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<WiltechsUserInfoDto> AuthenticateAsync(string authorization, CancellationToken cancellationToken)
    {
        return GetUserFromJwtToken(authorization) ?? await GetUserFromAuthorizationServerAsync(authorization, cancellationToken).ConfigureAwait(false);
    }

    private WiltechsUserInfoDto GetUserFromJwtToken(string authorization)
    {
        var token = authorization.Replace("Bearer ", "");
        
        var tokenHandler = new JwtSecurityTokenHandler();

        if (!tokenHandler.CanReadToken(token)) return null;
        
        foreach (var issuer in _wiltechsAuthenticationSettings.Issuers)
        {
            var validateParameter = new TokenValidationParameters
            {
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_wiltechsAuthenticationSettings.SymmetricKey))
            };

            try
            {
                var claimsPrincipal =
                    tokenHandler.ValidateToken(token, validateParameter, out _);
        
                return new WiltechsUserInfoDto
                {
                    UserId = Guid.Parse(claimsPrincipal.Claims.Single(x => x.Type == "UserId").Value),
                    Username = claimsPrincipal.Claims.First(x => x.Type == "UserName").Value,
                    AccessToken = token
                };
            }
            catch (Exception e)
            {
                // ignored
            }
        }
        
        return null;
    }
    
    private async Task<WiltechsUserInfoDto> GetUserFromAuthorizationServerAsync(string authorization, CancellationToken cancellationToken)
    {
        return await _wiltechsClient.GetUserInfoAsync(authorization, cancellationToken).ConfigureAwait(false);
    }
}