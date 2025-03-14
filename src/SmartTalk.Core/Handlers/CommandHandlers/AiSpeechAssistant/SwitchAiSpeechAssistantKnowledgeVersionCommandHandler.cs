using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class SwitchAiSpeechAssistantKnowledgeVersionCommandHandler : ICommandHandler<SwitchAiSpeechAssistantKnowledgeVersionCommand, SwitchAiSpeechAssistantKnowledgeVersionResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public SwitchAiSpeechAssistantKnowledgeVersionCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<SwitchAiSpeechAssistantKnowledgeVersionResponse> Handle(IReceiveContext<SwitchAiSpeechAssistantKnowledgeVersionCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.SwitchAiSpeechAssistantKnowledgeVersionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}