using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class CreatePosCompanyStoreCommandHandler : ICommandHandler<CreatePosCompanyStoreCommand, CreatePosCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public CreatePosCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<CreatePosCompanyStoreResponse> Handle(IReceiveContext<CreatePosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.CreatePosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}