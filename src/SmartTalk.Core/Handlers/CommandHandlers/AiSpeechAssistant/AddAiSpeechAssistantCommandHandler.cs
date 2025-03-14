using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class AddAiSpeechAssistantCommandHandler : ICommandHandler<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public AddAiSpeechAssistantCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<AddAiSpeechAssistantResponse> Handle(IReceiveContext<AddAiSpeechAssistantCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.AddAiSpeechAssistantAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}