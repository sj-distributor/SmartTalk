using Serilog;
using Hangfire;
using Newtonsoft.Json;
using Hangfire.Correlate;
using Hangfire.Pro.Redis;
using SmartTalk.Core.Jobs;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Settings.Caching;

namespace SmartTalk.Api.Extensions;

public static class HangfireExtension
{
    public static void AddHangfireInternal(this IServiceCollection services, IConfiguration configuration)
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

        services.AddHangfireServer(opt =>
        {
            opt.ServerTimeout = TimeSpan.FromHours(2);
            opt.Queues = new[]
            {
                HangfireConstants.DefaultQueue,
                HangfireConstants.InternalHostingRestaurant
            };
        });
        
        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 5;
            opt.Queues = new[] { HangfireConstants.InternalHostingPhoneOrder };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingPhoneOrder.ToUpper()}-{Guid.NewGuid()}";
        });
        
        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 5;
            opt.Queues = new[] { HangfireConstants.InternalHostingSipServer };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingSipServer.ToUpper()}-{Guid.NewGuid()}";
        });
        
        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 5;
            opt.Queues = new[] { HangfireConstants.InternalHostingFfmpeg };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingFfmpeg.ToUpper()}-{Guid.NewGuid()}";
        });
        
        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 50;
            opt.Queues = new[] { HangfireConstants.InternalHostingTransfer };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingTransfer.ToUpper()}-{Guid.NewGuid()}";
        });
        
        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 2;
            opt.Queues = new[] { HangfireConstants.InternalHostingTestingSalesPhoneOrder };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingTestingSalesPhoneOrder.ToUpper()}-{Guid.NewGuid()}";
        });

        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 30;
            opt.Queues = new[] { HangfireConstants.InternalHostingAutoTestCallRecordSync };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingAutoTestCallRecordSync.ToUpper()}-{Guid.NewGuid()}";
        });
    }
    
    public static void UseHangfireInternal(this IApplicationBuilder app)
    {
        app.UseHangfireDashboard(options: new DashboardOptions
        {
            IgnoreAntiforgeryToken = true
        });

        app.ScanHangfireRecurringJobs();
    }

    public static void ScanHangfireRecurringJobs(this IApplicationBuilder app)
    {
        var backgroundJobClient = app.ApplicationServices.GetRequiredService<ISmartTalkBackgroundJobClient>();

        var recurringJobTypes = typeof(IRecurringJob).Assembly.GetTypes().Where(type => type.IsClass && typeof(IRecurringJob).IsAssignableFrom(type)).ToList();

        foreach (var type in recurringJobTypes)
        {
            var job = (IRecurringJob) app.ApplicationServices.GetRequiredService(type);

            if (string.IsNullOrEmpty(job.CronExpression))
            {
                Log.Error("Recurring Job Cron Expression Empty, {Job}", job.GetType().FullName);
                continue;
            }

            backgroundJobClient.AddOrUpdateRecurringJob<IJobSafeRunner>(job.JobId, r => r.Run(job.JobId, type), job.CronExpression, job.TimeZone);
        }
    }
}