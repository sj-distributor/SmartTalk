using SmartTalk.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Api.Authentication.ApiKey;

namespace SmartTalk.Api.Extensions;

public static class AuthenticationExtension
{
    public static void AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationSchemeConstants.ApiKeyAuthenticationScheme;
                options.DefaultChallengeScheme = AuthenticationSchemeConstants.ApiKeyAuthenticationScheme;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                AuthenticationSchemeConstants.ApiKeyAuthenticationScheme, _ => { });
        
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                AuthenticationSchemeConstants.ApiKeyAuthenticationScheme).RequireAuthenticatedUser().Build();
        });
    }
}