using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class CreateCompanyStoreCommandHandler : ICommandHandler<CreateCompanyStoreCommand, CreateCompanyStoreResponse>
{
    private readonly IPosService _posService;

    public CreateCompanyStoreCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<CreateCompanyStoreResponse> Handle(IReceiveContext<CreateCompanyStoreCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.CreateCompanyStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}