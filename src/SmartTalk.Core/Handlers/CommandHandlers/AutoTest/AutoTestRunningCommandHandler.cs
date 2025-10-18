using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class AutoTestRunningCommandHandler : ICommandHandler<AutoTestRunningCommand, AutoTestRunningResponse>
{
    private readonly IAutoTestService _autoTestService;

    public AutoTestRunningCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<AutoTestRunningResponse> Handle(IReceiveContext<AutoTestRunningCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.AutoTestRunningAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
