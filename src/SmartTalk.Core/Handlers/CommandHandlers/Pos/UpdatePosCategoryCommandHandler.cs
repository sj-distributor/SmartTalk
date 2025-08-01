using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosCategoryCommandHandler : ICommandHandler<UpdatePosCategoryCommand, UpdatePosCategoryResponse>
{
    private readonly IPosService _service;

    public UpdatePosCategoryCommandHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<UpdatePosCategoryResponse> Handle(IReceiveContext<UpdatePosCategoryCommand> context, CancellationToken cancellationToken)
    {
        return await _service.UpdatePosCategoryAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}