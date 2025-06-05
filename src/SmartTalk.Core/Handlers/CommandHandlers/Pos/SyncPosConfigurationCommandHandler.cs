using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class SyncPosConfigurationCommandHandler: ICommandHandler<SyncPosConfigurationCommand, SyncPosConfigurationResponse>
{
    private readonly IPosService _posService;

    public SyncPosConfigurationCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<SyncPosConfigurationResponse> Handle(IReceiveContext<SyncPosConfigurationCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.SyncPosConfigurationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}