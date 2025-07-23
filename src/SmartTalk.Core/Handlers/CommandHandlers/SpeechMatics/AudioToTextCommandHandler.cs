using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Core.Handlers.CommandHandlers.SpeechMatics;

public class AudioToTextCommandHandler:  ICommandHandler<AudioToTextCommand>
{
    private readonly ISpeechMaticsService _speechMaticsService;

    public AudioToTextCommandHandler(ISpeechMaticsService speechMaticsService)
    {
        _speechMaticsService = speechMaticsService;
    }

    public async Task Handle( IReceiveContext<AudioToTextCommand>context, CancellationToken cancellationToken = default)
    {
        await _speechMaticsService.AudioToTextAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}