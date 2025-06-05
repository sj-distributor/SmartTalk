using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdatePosCompanyStoreStatusCommandHandler : ICommandHandler<UpdatePosCompanyStoreStatusCommand, UpdatePosCompanyStoreStatusResponse>
{
    private readonly IPosService _posService;

    public UpdatePosCompanyStoreStatusCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<UpdatePosCompanyStoreStatusResponse> Handle(IReceiveContext<UpdatePosCompanyStoreStatusCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UpdatePosCompanyStoreStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}