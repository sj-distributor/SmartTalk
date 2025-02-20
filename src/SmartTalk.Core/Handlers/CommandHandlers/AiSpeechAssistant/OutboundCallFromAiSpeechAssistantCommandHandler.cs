using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class OutboundCallFromAiSpeechAssistantCommandHandler : ICommandHandler<OutboundCallFromAiSpeechAssistantCommand, OutboundCallFromAiSpeechAssistantResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public OutboundCallFromAiSpeechAssistantCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }
    
    public async Task<OutboundCallFromAiSpeechAssistantResponse> Handle(IReceiveContext<OutboundCallFromAiSpeechAssistantCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.OutboundCallFromAiSpeechAssistantAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}