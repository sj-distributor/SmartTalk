using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UnbindPosCompanyStoreCommandHandler : ICommandHandler<UnbindPosCompanyStoreCommand, UnbindPosCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public UnbindPosCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<UnbindPosCompanyStoreResponse> Handle(IReceiveContext<UnbindPosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UnbindPosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}