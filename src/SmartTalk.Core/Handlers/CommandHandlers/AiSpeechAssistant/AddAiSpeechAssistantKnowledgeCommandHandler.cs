using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class AddAiSpeechAssistantKnowledgeCommandHandler : ICommandHandler<AddAiSpeechAssistantKnowledgeCommand, AddAiSpeechAssistantKnowledgeResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public AddAiSpeechAssistantKnowledgeCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<AddAiSpeechAssistantKnowledgeResponse> Handle(IReceiveContext<AddAiSpeechAssistantKnowledgeCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _aiSpeechAssistantService.AddAiSpeechAssistantKnowledgeAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
        
        return new AddAiSpeechAssistantKnowledgeResponse
        {
            Data = @event.LatestKnowledge
        };
    }
}