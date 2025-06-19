using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class AddAiSpeechAssistantSessionRequestHandler : ICommandHandler<AddAiSpeechAssistantSessionCommand, AddAiSpeechAssistantSessionResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public AddAiSpeechAssistantSessionRequestHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<AddAiSpeechAssistantSessionResponse> Handle(IReceiveContext<AddAiSpeechAssistantSessionCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.AddAiSpeechAssistantSessionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}