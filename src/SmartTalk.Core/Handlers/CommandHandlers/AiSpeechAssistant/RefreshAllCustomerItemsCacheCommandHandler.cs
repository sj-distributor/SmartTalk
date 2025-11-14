using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class RefreshAllCustomerItemsCacheCommandHandler : ICommandHandler<RefreshAllCustomerItemsCacheCommand>
{
    private readonly ISpeechMaticsJobService _speechMaticsJobService;
    
    public RefreshAllCustomerItemsCacheCommandHandler(ISpeechMaticsJobService speechMaticsJobService)
    {
        _speechMaticsJobService = speechMaticsJobService;
    }
    
    public async Task Handle(IReceiveContext<RefreshAllCustomerItemsCacheCommand> context, CancellationToken cancellationToken)
    {
        await _speechMaticsJobService.ScheduleRefreshCustomerItemsCacheAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}