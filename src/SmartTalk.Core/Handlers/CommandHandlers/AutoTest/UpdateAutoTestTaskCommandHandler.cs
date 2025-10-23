using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class UpdateAutoTestTestTaskCommandHandler : ICommandHandler<UpdateAutoTestTaskCommand, UpdateAutoTestTaskResponse>
{
    private readonly IAutoTestService _autoTestService;

    public UpdateAutoTestTestTaskCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<UpdateAutoTestTaskResponse> Handle(IReceiveContext<UpdateAutoTestTaskCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.UpdateAutoTestTaskAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}