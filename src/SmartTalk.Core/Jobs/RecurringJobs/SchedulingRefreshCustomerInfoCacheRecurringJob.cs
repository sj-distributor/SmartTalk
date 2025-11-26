using Mediator.Net;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingRefreshCustomerInfoCacheRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingRefreshCustomerInfoCacheRecurringJobExpressionSetting _settings;

    public SchedulingRefreshCustomerInfoCacheRecurringJob(IMediator mediator, SchedulingRefreshCustomerInfoCacheRecurringJobExpressionSetting settings)
    {
        _mediator = mediator;
        _settings = settings;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new RefreshAllCustomerInfoCacheCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(SchedulingRefreshCustomerInfoCacheRecurringJob);

    public string CronExpression => _settings.Value;
}