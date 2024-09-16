using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.Restaurants;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingSyncRestaurantMenuRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public SchedulingSyncRestaurantMenuRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new SchedulingSyncRestaurantMenuCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingSyncRestaurantMenuRecurringJob);

    public string CronExpression => Cron.Daily();

    public string TimeZone = "Asia/Shanghai";
}