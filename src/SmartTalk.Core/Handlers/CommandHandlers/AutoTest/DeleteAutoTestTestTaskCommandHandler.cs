using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class DeleteAutoTestTestTaskCommandHandler : ICommandHandler<DeleteAutoTestTestTaskCommand, DeleteAutoTestTestTaskResponse>
{
    private readonly IAutoTestService _autoTestService;

    public DeleteAutoTestTestTaskCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<DeleteAutoTestTestTaskResponse> Handle(IReceiveContext<DeleteAutoTestTestTaskCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.DeleteAutoTestTestTaskAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
