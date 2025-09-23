using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.Pos;

namespace SmartTalk.Core.Handlers.CommandHandlers.Pos;

public class UpdateStoreCustomerCommandHandler : ICommandHandler<UpdateStoreCustomerCommand, UpdateStoreCustomerResponse>
{
    private readonly IPosService _posService;

    public UpdateStoreCustomerCommandHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<UpdateStoreCustomerResponse> Handle(IReceiveContext<UpdateStoreCustomerCommand> context, CancellationToken cancellationToken)
    {
        return await _posService.UpdateStoreCustomerAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}