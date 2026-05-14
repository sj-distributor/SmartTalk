using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class AddAiSpeechAssistantKnowledgeDetailCommandHandler
    : ICommandHandler<AddAiSpeechAssistantKnowledgeDetailCommand, AddAiSpeechAssistantKnowledgeDetailResponse>
{
    private readonly IAiSpeechAssistantService _service;

    public AddAiSpeechAssistantKnowledgeDetailCommandHandler(IAiSpeechAssistantService service)
    {
        _service = service;
    }

    public async Task<AddAiSpeechAssistantKnowledgeDetailResponse> Handle(IReceiveContext<AddAiSpeechAssistantKnowledgeDetailCommand> context,
        CancellationToken cancellationToken)
    {
        return await _service.AddAiSpeechAssistantKnowledgeDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}