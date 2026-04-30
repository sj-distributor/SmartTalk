using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class DetectAudioLanguageCommandHandler : ICommandHandler<DetectAudioLanguageCommand, DetectAudioLanguageResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public DetectAudioLanguageCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<DetectAudioLanguageResponse> Handle(IReceiveContext<DetectAudioLanguageCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.DetectAudioLanguageAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
