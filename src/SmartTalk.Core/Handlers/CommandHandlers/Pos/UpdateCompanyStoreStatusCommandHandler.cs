using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdateCompanyStoreStatusCommandHandler : ICommandHandler<UpdateCompanyStoreStatusCommand, UpdateCompanyStoreStatusResponse>
{
    private readonly IPosService _posService;

    public UpdateCompanyStoreStatusCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<UpdateCompanyStoreStatusResponse> Handle(IReceiveContext<UpdateCompanyStoreStatusCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UpdateCompanyStoreStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}