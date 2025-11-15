using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class CreateAutoTestTaskCommandHandler : ICommandHandler<CreateAutoTestTaskCommand, CreateAutoTestTaskResponse>
{
    private readonly IAutoTestService _autoTestService;

    public CreateAutoTestTaskCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<CreateAutoTestTaskResponse> Handle(IReceiveContext<CreateAutoTestTaskCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.CreateAutoTestTaskAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
