using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Messages.Commands.AiResourceSync;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiResourceSync;

public class SchedulingRefreshCrmCustomerContactPhoneMapCommandHandler : ICommandHandler<SchedulingRefreshCrmCustomerContactPhoneMapCommand>
{
    private readonly IAiResourceSyncProcessJobService _aiResourceSyncProcessJobService;

    public SchedulingRefreshCrmCustomerContactPhoneMapCommandHandler(IAiResourceSyncProcessJobService aiResourceSyncProcessJobService)
    {
        _aiResourceSyncProcessJobService = aiResourceSyncProcessJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingRefreshCrmCustomerContactPhoneMapCommand> context, CancellationToken cancellationToken)
    {
        await _aiResourceSyncProcessJobService.RefreshCrmCustomerContactPhoneMapsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
