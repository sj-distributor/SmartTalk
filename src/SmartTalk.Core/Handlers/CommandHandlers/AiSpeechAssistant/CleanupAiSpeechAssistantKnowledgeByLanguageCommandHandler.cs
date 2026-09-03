using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class CleanupAiSpeechAssistantKnowledgeByLanguageCommandHandler : ICommandHandler<CleanupAiSpeechAssistantKnowledgeByLanguageCommand>
{
    private readonly IAiSpeechAssistantProcessJobService _processJobService;

    public CleanupAiSpeechAssistantKnowledgeByLanguageCommandHandler(IAiSpeechAssistantProcessJobService processJobService)
    {
        _processJobService = processJobService;
    }

    public async Task Handle(IReceiveContext<CleanupAiSpeechAssistantKnowledgeByLanguageCommand> context, CancellationToken cancellationToken)
    {
        await _processJobService.CleanupAiSpeechAssistantKnowledgeByLanguageAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
