using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class DeleteCompanyStoreCommandHandler : ICommandHandler<DeleteCompanyStoreCommand, DeleteCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public DeleteCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<DeleteCompanyStoreResponse> Handle(IReceiveContext<DeleteCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.DeleteCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}