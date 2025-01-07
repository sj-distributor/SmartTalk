using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SipServer;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Core.Handlers.CommandHandlers.SipServer;

public class AddSipHostServersCommandHandler : ICommandHandler<AddSipHostServersCommand, AddSipHostServersResponse>
{
    private readonly ISipServerService _sipServerService;

    public AddSipHostServersCommandHandler(ISipServerService sipServerService)
    {
        _sipServerService = sipServerService;
    }
    
    public async Task<AddSipHostServersResponse> Handle(IReceiveContext<AddSipHostServersCommand> context, CancellationToken cancellationToken)
    {
        return await _sipServerService.AddSipHostServersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}