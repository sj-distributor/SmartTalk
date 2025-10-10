using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class AdjustPosMenuContentSortCommandHandler : ICommandHandler<AdjustPosMenuContentSortCommand, AdjustPosMenuContentSortResponse>
{
    private readonly IPosService _posService;

    public AdjustPosMenuContentSortCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<AdjustPosMenuContentSortResponse> Handle(IReceiveContext<AdjustPosMenuContentSortCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.AdjustPosMenuContentSortAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}