using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class BindPosCompanyStoreCommandHandler : ICommandHandler<BindPosCompanyStoreCommand, BindPosCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public BindPosCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }
    public async Task<BindPosCompanyStoreResponse> Handle(IReceiveContext<BindPosCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.BindPosCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}