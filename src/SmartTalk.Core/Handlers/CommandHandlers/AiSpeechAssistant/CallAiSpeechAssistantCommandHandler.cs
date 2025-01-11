using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class CallAiSpeechAssistantCommandHandler : ICommandHandler<CallAiSpeechAssistantCommand, CallAiSpeechAssistantResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public CallAiSpeechAssistantCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public Task<CallAiSpeechAssistantResponse> Handle(IReceiveContext<CallAiSpeechAssistantCommand> context, CancellationToken cancellationToken)
    {
        var response = _aiSpeechAssistantService.CallAiSpeechAssistant(context.Message);

        return Task.FromResult(response);
    }
}