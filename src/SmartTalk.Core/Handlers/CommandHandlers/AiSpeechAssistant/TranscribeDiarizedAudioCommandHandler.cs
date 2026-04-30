using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class TranscribeDiarizedAudioCommandHandler : ICommandHandler<TranscribeDiarizedAudioCommand, TranscribeDiarizedAudioResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public TranscribeDiarizedAudioCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<TranscribeDiarizedAudioResponse> Handle(IReceiveContext<TranscribeDiarizedAudioCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.TranscribeDiarizedAudioAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
