using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class TranscribeAndDetectAudioLanguageCommandHandler : ICommandHandler<TranscribeAndDetectAudioLanguageCommand, TranscribeAndDetectAudioLanguageResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public TranscribeAndDetectAudioLanguageCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<TranscribeAndDetectAudioLanguageResponse> Handle(IReceiveContext<TranscribeAndDetectAudioLanguageCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.TranscribeAndDetectAudioLanguageAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
