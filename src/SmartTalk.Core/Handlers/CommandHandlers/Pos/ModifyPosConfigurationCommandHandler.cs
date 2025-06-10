using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class ModifyPosConfigurationCommandHandler : ICommandHandler<ModifyPosConfigurationCommand>
{
    private readonly IPosService _posService;

    public ModifyPosConfigurationCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task Handle(IReceiveContext<ModifyPosConfigurationCommand> context, CancellationToken cancellationToken)
    {
        await _posService.ModifyPosConfigurationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}