using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class DeleteAutoTestDataSetCommandHandler : ICommandHandler<DeleteAutoTestDataSetCommand, DeleteAutoTestDataSetResponse>
{
    private readonly IAutoTestService _autoTestService;

    public DeleteAutoTestDataSetCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<DeleteAutoTestDataSetResponse> Handle(IReceiveContext<DeleteAutoTestDataSetCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.DeleteAutoTestDataSetAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}