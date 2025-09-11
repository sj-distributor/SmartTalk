using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdateCompanyStoreCommandHandler : ICommandHandler<UpdateCompanyStoreCommand, UpdateCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public UpdateCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<UpdateCompanyStoreResponse> Handle(IReceiveContext<UpdateCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UpdateCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}