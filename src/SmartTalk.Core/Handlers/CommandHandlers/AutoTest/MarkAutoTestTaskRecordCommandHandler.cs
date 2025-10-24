using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class MarkAutoTestTaskRecordCommandHandler : ICommandHandler<MarkAutoTestTaskRecordCommand, MarkAutoTestTaskRecordResponse>
{
    private readonly IAutoTestService _autoTestService;

    public MarkAutoTestTaskRecordCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<MarkAutoTestTaskRecordResponse> Handle(IReceiveContext<MarkAutoTestTaskRecordCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.MarkAutoTestTaskRecordAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}