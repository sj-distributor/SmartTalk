using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.HrInterView;
using SmartTalk.Messages.Commands.HrInterView;

namespace SmartTalk.Core.Handlers.CommandHandlers.HrInterView;

public class ConnectHrInterViewCommandHandler : ICommandHandler<ConnectHrInterViewCommand>
{
    private readonly IHrInterViewService _hrInterViewService;

    public ConnectHrInterViewCommandHandler(IHrInterViewService hrInterViewService)
    {
        _hrInterViewService = hrInterViewService;
    }

    public async Task Handle(IReceiveContext<ConnectHrInterViewCommand> context, CancellationToken cancellationToken)
    {
        await _hrInterViewService.ConnectWebSocketAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}