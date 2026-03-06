using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class SyncAiSpeechAssistantKnowledgePromptCommandHandler : ICommandHandler<SyncAiSpeechAssistantKnowledgePromptCommand>
{
    private readonly IAiSpeechAssistantProcessJobService _processJobService;

    public SyncAiSpeechAssistantKnowledgePromptCommandHandler(IAiSpeechAssistantProcessJobService processJobService)
    {
        _processJobService = processJobService;
    }

    public async Task Handle(IReceiveContext<SyncAiSpeechAssistantKnowledgePromptCommand> context, CancellationToken cancellationToken)
    {
        await _processJobService.SyncAiSpeechAssistantKnowledgePromptAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}

