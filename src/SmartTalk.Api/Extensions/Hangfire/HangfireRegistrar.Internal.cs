using Hangfire;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Jobs;
using SmartTalk.Core.Services.Jobs;

namespace SmartTalk.Api.Extensions.Hangfire;

public class InternalHangfireRegistrar : HangfireRegistrarBase
{
    public override void RegisterHangfire(IServiceCollection services, IConfiguration configuration)
    {
        base.RegisterHangfire(services, configuration);

        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 30;
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
            opt.WorkerCount = 50;
            opt.Queues = new[] { HangfireConstants.InternalHostingRecordPhoneCall };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingRecordPhoneCall.ToUpper()}-{Guid.NewGuid()}";
        });

        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = 20;
            opt.Queues = new[] { HangfireConstants.InternalHostingCaCheKnowledgeVariable };
            opt.ServerName = $"DEPLOY-{HangfireConstants.InternalHostingCaCheKnowledgeVariable.ToUpper()}-{Guid.NewGuid()}";
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

    public override void ApplyHangfire(IApplicationBuilder app, IConfiguration configuration)
    {
        base.ApplyHangfire(app, configuration);

        ScanHangfireRecurringJobs(app);

        app.UseHangfireDashboard(options: new DashboardOptions
        {
            IgnoreAntiforgeryToken = true
        });
    }

    private static void ScanHangfireRecurringJobs(IApplicationBuilder app)
    {
        var backgroundJobClient = app.ApplicationServices.GetRequiredService<ISmartTalkBackgroundJobClient>();

        var recurringJobTypes = typeof(IRecurringJob).Assembly.GetTypes().Where(type => type.IsClass && typeof(IRecurringJob).IsAssignableFrom(type)).ToList();

        foreach (var type in recurringJobTypes)
        {
            var job = (IRecurringJob)app.ApplicationServices.GetRequiredService(type);

            if (string.IsNullOrEmpty(job.CronExpression))
            {
                Log.Error("Recurring Job Cron Expression Empty, {Job}", job.GetType().FullName);
                continue;
            }

            backgroundJobClient.AddOrUpdateRecurringJob<IJobSafeRunner>(job.JobId, r => r.Run(job.JobId, type), job.CronExpression, job.TimeZone);
        }
    }
}