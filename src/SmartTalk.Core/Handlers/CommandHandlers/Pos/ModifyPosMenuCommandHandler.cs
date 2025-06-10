using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class ModifyPosMenuCommandHandler : ICommandHandler<ModifyPosMenuCommand>
{
    private readonly IPosService _posService;

    public ModifyPosMenuCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task Handle(IReceiveContext<ModifyPosMenuCommand> context, CancellationToken cancellationToken)
    {
        await _posService.ModifyPosMenuAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}