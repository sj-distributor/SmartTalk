using System.Text;
using SmartTalk.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Api.Authentication.ApiKey;
using SmartTalk.Api.Authentication.OME;
using SmartTalk.Api.Authentication.Wiltechs;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Settings.Authentication;
using SmartTalk.Core.Settings.System;
using SmartTalk.Messages.Enums.System;

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
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateLifetime = false,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(new JwtSymmetricKeySetting(configuration).Value
                                .PadRight(256 / 8, '\0')))
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                AuthenticationSchemeConstants.ApiKeyAuthenticationScheme, _ => { })
            .AddScheme<WiltechsAuthenticationOptions, WiltechsAuthenticationHandler>(
                AuthenticationSchemeConstants.WiltechsAuthenticationScheme, options =>
                {
                    options.Issuers = configuration["Authentication:Wiltechs:Issuers"]?.Split(",").ToList();
                    options.Authority = configuration["Authentication:Wiltechs:Authority"];
                    options.SymmetricKey = configuration["Authentication:Wiltechs:SymmetricKey"];
                })
            .AddScheme<OMEAuthenticationOptions, OMEAuthenticationHandler>(
                AuthenticationSchemeConstants.OMEAuthenticationScheme, options =>
                {
                    options.Authority = configuration["Authentication:OME:Authority"];
                    options.AppId = configuration["Authentication:OME:AppId"];
                    options.AppSecret = configuration["Authentication:OME:AppSecret"];
                });
        
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                AuthenticationSchemeConstants.ApiKeyAuthenticationScheme,
                AuthenticationSchemeConstants.WiltechsAuthenticationScheme,
                AuthenticationSchemeConstants.OMEAuthenticationScheme).RequireAuthenticatedUser().Build();
        });
        
        RegisterCurrentUser(services, configuration);
    }
    
    private static void RegisterCurrentUser(IServiceCollection services, IConfiguration configuration)
    {
        var appType = new ApiRunModeSetting(configuration).Value;

        switch (appType)
        {
            case ApiRunMode.Api:
                services.AddScoped<ICurrentUser, ApiUser>();
                break;
            case ApiRunMode.Internal:
                services.AddScoped<ICurrentUser, InternalUser>();
                break;
        }
    }
}