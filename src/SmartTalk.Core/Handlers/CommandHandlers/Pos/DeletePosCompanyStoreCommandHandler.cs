using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class DeletePosCompanyStoreCommandHandler : ICommandHandler<DeletePosCompanyStoreCommand, DeletePosCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public DeletePosCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<DeletePosCompanyStoreResponse> Handle(IReceiveContext<DeletePosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.DeletePosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}