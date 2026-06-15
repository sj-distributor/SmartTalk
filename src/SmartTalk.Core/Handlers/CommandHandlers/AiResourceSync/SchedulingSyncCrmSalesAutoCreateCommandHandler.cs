using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Commands.Sales;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiResourceSync;

public class SchedulingSyncCrmSalesAutoCreateCommandHandler : ICommandHandler<SchedulingSyncCrmSalesAutoCreateCommand>
{
    private readonly IAiResourceSyncProcessJobService _aiResourceSyncProcessJobService ;
    
    public SchedulingSyncCrmSalesAutoCreateCommandHandler(IAiResourceSyncProcessJobService aiResourceSyncProcessJobService)
    {
        _aiResourceSyncProcessJobService = aiResourceSyncProcessJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingSyncCrmSalesAutoCreateCommand> context, CancellationToken cancellationToken)
    {
        await _aiResourceSyncProcessJobService.AiResourceSyncAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
