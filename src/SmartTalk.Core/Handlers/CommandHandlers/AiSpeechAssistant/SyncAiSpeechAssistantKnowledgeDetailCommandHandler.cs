using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class SyncAiSpeechAssistantKnowledgeDetailCommandHandler : ICommandHandler<SyncAiSpeechAssistantKnowledgeDetailCommand>
{
    private readonly IAiSpeechAssistantProcessJobService _processJobService;

    public SyncAiSpeechAssistantKnowledgeDetailCommandHandler(IAiSpeechAssistantProcessJobService processJobService)
    {
        _processJobService = processJobService;
    }

    public async Task Handle(IReceiveContext<SyncAiSpeechAssistantKnowledgeDetailCommand> context, CancellationToken cancellationToken)
    {
        await _processJobService.SyncAiSpeechAssistantKnowledgeDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
