using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class DeleteAutoTestTaskCommandHandler : ICommandHandler<DeleteAutoTestTaskCommand, DeleteAutoTestTaskResponse>
{
    private readonly IAutoTestService _autoTestService;

    public DeleteAutoTestTaskCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<DeleteAutoTestTaskResponse> Handle(IReceiveContext<DeleteAutoTestTaskCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.DeleteAutoTestTaskAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
