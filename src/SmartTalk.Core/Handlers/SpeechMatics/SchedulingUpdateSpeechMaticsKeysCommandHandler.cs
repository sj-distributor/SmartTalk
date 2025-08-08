using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Core.Handlers.CommandHandlers.SpeechMatics;

public class SchedulingUpdateSpeechMaticsKeysCommandHandler : ICommandHandler<SchedulingUpdateSpeechMaticsKeysCommand>
{
    private readonly ISpeechMaticsJobService _speechMaticsJobService;

    public SchedulingUpdateSpeechMaticsKeysCommandHandler(ISpeechMaticsJobService speechMaticsJobService)
    {
        _speechMaticsJobService = speechMaticsJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingUpdateSpeechMaticsKeysCommand> context, CancellationToken cancellationToken)
    {
        await _speechMaticsJobService.UploadSpeechMaticsKeysAsync(cancellationToken).ConfigureAwait(false);
    }
}