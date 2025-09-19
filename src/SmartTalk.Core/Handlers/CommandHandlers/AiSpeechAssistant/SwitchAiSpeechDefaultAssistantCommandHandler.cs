using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class SwitchAiSpeechDefaultAssistantCommandHandler : ICommandHandler<SwitchAiSpeechDefaultAssistantCommand, SwitchAiSpeechDefaultAssistantResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public SwitchAiSpeechDefaultAssistantCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<SwitchAiSpeechDefaultAssistantResponse> Handle(IReceiveContext<SwitchAiSpeechDefaultAssistantCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.SwitchAiSpeechDefaultAssistantAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}