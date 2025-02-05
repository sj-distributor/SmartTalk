using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class RecordAiSpeechAssistantCallCommandHandler : ICommandHandler<RecordAiSpeechAssistantCallCommand>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public RecordAiSpeechAssistantCallCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task Handle(IReceiveContext<RecordAiSpeechAssistantCallCommand> context, CancellationToken cancellationToken)
    {
        await _aiSpeechAssistantService.RecordAiSpeechAssistantCallAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}