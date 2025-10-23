using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class CreateAutoTestTestTaskCommandHandler : ICommandHandler<CreateAutoTestTestTaskCommand, CreateAutoTestTestTaskResponse>
{
    private readonly IAutoTestService _autoTestService;

    public CreateAutoTestTestTaskCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<CreateAutoTestTestTaskResponse> Handle(IReceiveContext<CreateAutoTestTestTaskCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.CreateAutoTestTestTaskAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
