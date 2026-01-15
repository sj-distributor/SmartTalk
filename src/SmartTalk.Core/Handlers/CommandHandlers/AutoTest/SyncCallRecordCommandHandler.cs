using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class SyncCallRecordCommandHandler : ICommandHandler<SyncCallRecordCommand>
{
    private readonly IAutoTestProcessJobService _autoTestProcessJobService;

    public SyncCallRecordCommandHandler(IAutoTestProcessJobService autoTestProcessJobService)
    {
        _autoTestProcessJobService = autoTestProcessJobService;
    }

    public async Task Handle(IReceiveContext<SyncCallRecordCommand> context, CancellationToken cancellationToken)
    {
        await _autoTestProcessJobService.SyncCallRecordsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}