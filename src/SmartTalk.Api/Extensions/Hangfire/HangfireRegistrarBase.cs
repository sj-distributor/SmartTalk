using Hangfire;
using Hangfire.Correlate;
using Hangfire.Pro.Redis;
using Newtonsoft.Json;
using SmartTalk.Core.Settings.Caching;

namespace SmartTalk.Api.Extensions.Hangfire;

public class HangfireRegistrarBase : IHangfireRegistrar
{
    public virtual void RegisterHangfire(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire((sp, c) =>
        {
            c.UseCorrelate(sp);
            c.UseMaxArgumentSizeToRender(int.MaxValue);
            c.UseFilter(new AutomaticRetryAttribute { Attempts = 0 });
            c.UseRedisStorage(new RedisCacheConnectionStringSetting(configuration).Value,
                new RedisStorageOptions { MaxSucceededListLength = 500000, MaxDeletedListLength = 10000 }).WithJobExpirationTimeout(TimeSpan.FromDays(1));
            c.UseSerializerSettings(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        });
    }

    public virtual void ApplyHangfire(IApplicationBuilder app, IConfiguration configuration)
    {
    }
}
