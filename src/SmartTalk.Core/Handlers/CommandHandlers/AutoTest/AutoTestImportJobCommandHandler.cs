using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class AutoTestImportJobCommandHandler : ICommandHandler<AutoTestImportJobCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAutoTestImportJobService _autoTestImportJobService;

    public AutoTestImportJobCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient, IAutoTestImportJobService autoTestImportJobService)
    {
        _backgroundJobClient = backgroundJobClient;
        _autoTestImportJobService = autoTestImportJobService;
    }

    public async Task Handle(IReceiveContext<AutoTestImportJobCommand> context, CancellationToken cancellationToken)
    {
        var command = context.Message;
        
        _backgroundJobClient.Enqueue<IAutoTestImportJobService>(x 
            => x.HandleAutoTestImportDataJobAsync(command.CustomerId, command.StartDate, command.EndDate, command.ScenarioId, cancellationToken));
    }
}