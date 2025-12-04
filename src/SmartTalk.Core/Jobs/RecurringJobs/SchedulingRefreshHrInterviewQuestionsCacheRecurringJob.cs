using Mediator.Net;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Messages.Commands.Hr;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class SchedulingRefreshHrInterviewQuestionsCacheRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly SchedulingRefreshHrInterviewQuestionsCacheRecurringJobCronExpressionSetting _expressionSetting;

    public SchedulingRefreshHrInterviewQuestionsCacheRecurringJob(IMediator mediator, SchedulingRefreshHrInterviewQuestionsCacheRecurringJobCronExpressionSetting expressionSetting)
    {
        _mediator = mediator;
        _expressionSetting = expressionSetting;
    }

    public async Task Execute()
    {
        await _mediator.SendAsync(new RefreshHrInterviewQuestionsCacheCommand());
    }

    public string JobId => nameof(SchedulingRefreshHrInterviewQuestionsCacheRecurringJob);
    
    public string CronExpression => _expressionSetting.Value;
}