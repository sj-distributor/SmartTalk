using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantDynamicConfigCommandHandler
    : ICommandHandler<UpdateAiSpeechAssistantDynamicConfigCommand, UpdateAiSpeechAssistantDynamicConfigResponse>
{
    private readonly IAiSpeechAssistantService _service;

    public UpdateAiSpeechAssistantDynamicConfigCommandHandler(IAiSpeechAssistantService service)
    {
        _service = service;
    }

    public async Task<UpdateAiSpeechAssistantDynamicConfigResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantDynamicConfigCommand> context,
        CancellationToken cancellationToken)
    {
        return await _service.UpdateAiSpeechAssistantDynamicConfigAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
