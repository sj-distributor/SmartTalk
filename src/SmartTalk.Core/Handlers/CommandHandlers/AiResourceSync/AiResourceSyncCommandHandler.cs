using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Messages.Commands.AiResourceSync;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiResourceSync;

public class AiResourceSyncCommandHandler : ICommandHandler<AiResourceSyncCommand, AiResourceSyncResponse>
{
    private readonly IAiResourceSyncProcessJobService _aiResourceSyncProcessJobService;

    public AiResourceSyncCommandHandler(IAiResourceSyncProcessJobService aiResourceSyncProcessJobService)
    {
        _aiResourceSyncProcessJobService = aiResourceSyncProcessJobService;
    }

    public async Task<AiResourceSyncResponse> Handle(IReceiveContext<AiResourceSyncCommand> context, CancellationToken cancellationToken)
    {
        await _aiResourceSyncProcessJobService.ExecuteSyncCrmSalesAutoCreateAsync(context.Message, cancellationToken).ConfigureAwait(false);

        return new AiResourceSyncResponse();
    }
}
