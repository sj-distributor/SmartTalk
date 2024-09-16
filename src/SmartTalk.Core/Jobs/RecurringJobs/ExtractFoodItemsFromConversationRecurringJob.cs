using Hangfire;
using Mediator.Net;
using SmartTalk.Messages.Commands.Restaurants;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class ExtractFoodItemsFromConversationRecurringJob : IRecurringJob
{
    private readonly IMediator _mediator;

    public ExtractFoodItemsFromConversationRecurringJob(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new ExtractFoodItemsFromConversationCommand()).ConfigureAwait(false);
    }

    public string JobId => nameof(ExtractFoodItemsFromConversationCommand);

    public string CronExpression => Cron.Never();
}