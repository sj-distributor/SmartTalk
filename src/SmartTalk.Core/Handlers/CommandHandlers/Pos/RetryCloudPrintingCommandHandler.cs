using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class RetryCloudPrintingCommandHandler : ICommandHandler<RetryCloudPrintingCommand>
{
    private readonly IPosService _posService;

    public RetryCloudPrintingCommandHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task Handle(IReceiveContext<RetryCloudPrintingCommand> context, CancellationToken cancellationToken)
    {
        await _posService.RetryCloudPrintingAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}