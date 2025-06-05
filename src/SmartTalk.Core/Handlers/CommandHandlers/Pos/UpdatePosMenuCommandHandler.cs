using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosMenuCommandHandler : ICommandHandler<UpdatePosMenuCommand, UpdatePosMenuResponse>
{
    private readonly IPosService _service;

    public UpdatePosMenuCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<UpdatePosMenuResponse> Handle(IReceiveContext<UpdatePosMenuCommand> context, CancellationToken cancellationToken)
    {
        return await _service.UpdatePosMenuAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}