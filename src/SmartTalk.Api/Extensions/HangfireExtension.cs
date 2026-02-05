using SmartTalk.Api.Extensions.Hangfire;
using SmartTalk.Core.Settings.System;
using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Api.Extensions;

public static class HangfireExtension
{
    public static void AddHangfireInternal(this IServiceCollection services, IConfiguration configuration)
    {
        var hangfireRegistrar = FindRegistrar(configuration);

        hangfireRegistrar.RegisterHangfire(services, configuration);
    }

    public static void UseHangfireInternal(this IApplicationBuilder app, IConfiguration configuration)
    {
        var hangfireRegistrar = FindRegistrar(configuration);

        hangfireRegistrar.ApplyHangfire(app, configuration);
    }

    private static IHangfireRegistrar FindRegistrar(IConfiguration configuration)
    {
        var hangfireHosting = new HangfireHostingSetting(configuration).Value;
        
        return hangfireHosting switch
        {
            HangfireHosting.Api => new ApiHangfireRegistrar(),
            HangfireHosting.Internal => new InternalHangfireRegistrar()
        };
    }
}