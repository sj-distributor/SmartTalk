using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SipServer;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Core.Handlers.CommandHandlers.SipServer;

public class AddSipBackupServersCommandHandler : ICommandHandler<AddSipBackupServersCommand, AddSipBackupServersResponse>
{
    private readonly ISipServerService _sipServerService;

    public AddSipBackupServersCommandHandler(ISipServerService sipServerService)
    {
        _sipServerService = sipServerService;
    }
    
    public async Task<AddSipBackupServersResponse> Handle(IReceiveContext<AddSipBackupServersCommand> context, CancellationToken cancellationToken)
    {
        return await _sipServerService.AddSipBackupServersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}