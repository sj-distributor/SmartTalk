using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Handlers.CommandHandlers.Security;

public class GrantToPermissionAndSyncToAdministratorCommandHandler : ICommandHandler<AutomaticPermissionsPersistCommand>
{
    private readonly ISecurityProcessJobsService _securityProcessJobsService;

    public GrantToPermissionAndSyncToAdministratorCommandHandler(ISecurityProcessJobsService securityProcessJobsService)
    {
        _securityProcessJobsService = securityProcessJobsService;
    }

    public async Task Handle(IReceiveContext<AutomaticPermissionsPersistCommand> context, CancellationToken cancellationToken)
    {
        await _securityProcessJobsService.AutomaticPermissionsPersistAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}