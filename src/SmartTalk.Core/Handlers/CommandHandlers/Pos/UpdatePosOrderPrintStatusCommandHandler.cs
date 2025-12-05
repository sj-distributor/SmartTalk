using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosOrderPrintStatusCommandHandler : ICommandHandler<UpdatePosOrderPrintStatusCommand, UpdatePosOrderPrintStatusResponse>
{
    private readonly IPosService _posService;

    public UpdatePosOrderPrintStatusCommandHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<UpdatePosOrderPrintStatusResponse> Handle(IReceiveContext<UpdatePosOrderPrintStatusCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UpdatePosOrderPrintStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}