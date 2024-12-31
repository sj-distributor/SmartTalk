using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SipServer;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Core.Handlers.CommandHandlers.SipServer;

public class BackupSipServerDataCommandHandler : ICommandHandler<BackupSipServerDataCommand>
{
    private readonly ISipServerService _sipServerService;

    public BackupSipServerDataCommandHandler(ISipServerService sipServerService)
    {
        _sipServerService = sipServerService;
    }
    
    public async Task Handle(IReceiveContext<BackupSipServerDataCommand> context, CancellationToken cancellationToken)
    {
        await _sipServerService.BackupSipServerDataAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}