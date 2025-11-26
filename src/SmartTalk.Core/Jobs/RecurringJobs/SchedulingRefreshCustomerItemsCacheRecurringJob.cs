using Mediator.Net;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingRefreshCustomerItemsCacheRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingRefreshCustomerItemsCacheRecurringJobExpressionSetting _settings;

    public SchedulingRefreshCustomerItemsCacheRecurringJob(IMediator mediator, SchedulingRefreshCustomerItemsCacheRecurringJobExpressionSetting settings)
    {
        _mediator = mediator;
        _settings = settings;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new RefreshAllCustomerItemsCacheCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingRefreshCustomerItemsCacheRecurringJob);

    public string CronExpression => _settings.Value;
}