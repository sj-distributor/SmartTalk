using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class AutoTestImportJobCommandHandler : ICommandHandler<AutoTestImportJobCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IEnumerable<IAutoTestDataImportHandler> _handlers;

    public AutoTestImportJobCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient, IEnumerable<IAutoTestDataImportHandler> handlers)
    {
        _backgroundJobClient = backgroundJobClient;
        _handlers = handlers;
    }

    public async Task Handle(IReceiveContext<AutoTestImportJobCommand> context, CancellationToken cancellationToken)
    {
        var command = context.Message;

        var handler = _handlers.FirstOrDefault(x => x.ImportType == AutoTestImportDataRecordType.Api);
        if (handler == null) throw new InvalidOperationException("未找到 ApiDataImportHandler");

        var importParams = new Dictionary<string, object>
        {
            { "CustomerId", command.CustomerId },
            { "StartDate", command.StartDate },
            { "EndDate", command.EndDate },
            { "ScenarioId", command.ScenarioId }
        };

        _backgroundJobClient.Enqueue(() => handler.ImportAsync(importParams, cancellationToken));
    }
}