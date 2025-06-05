using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosCompanyStoreCommandHandler : ICommandHandler<UpdatePosCompanyStoreCommand, UpdatePosCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public UpdatePosCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<UpdatePosCompanyStoreResponse> Handle(IReceiveContext<UpdatePosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UpdatePosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}