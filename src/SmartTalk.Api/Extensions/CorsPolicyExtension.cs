using SmartTalk.Core.Settings.CorsPolicy;

namespace SmartTalk.Api.Extensions;

public static class CorsPolicyExtension
{
    public const string RealtimeAiWebRtcPocPolicy = "RealtimeAiWebRtcPoc";

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(
                policy =>
                {
                    policy.WithOrigins(new AllowableCorsOriginsSetting(configuration).Value)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });

            // Isolated POC policy: AllowAnyOrigin lets the standalone diagnostic HTML page
            // POST SDP without changing the existing application's CORS behavior.
            options.AddPolicy(
                RealtimeAiWebRtcPocPolicy,
                policy => policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("X-Realtime-Call-Id"));
        });
        
        return services;
    }
}
