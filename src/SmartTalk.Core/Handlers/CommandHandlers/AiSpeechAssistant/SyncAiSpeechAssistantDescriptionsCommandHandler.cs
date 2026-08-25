using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class SyncAiSpeechAssistantDescriptionsCommandHandler : ICommandHandler<SyncAiSpeechAssistantDescriptionCommand, SyncAiSpeechAssistantDescriptionsResponse>
{
    private readonly IAiSpeechAssistantService _service;

    public SyncAiSpeechAssistantDescriptionsCommandHandler(IAiSpeechAssistantService service)
    {
        _service = service;
    }

    public async Task<SyncAiSpeechAssistantDescriptionsResponse> Handle(IReceiveContext<SyncAiSpeechAssistantDescriptionCommand> context, CancellationToken cancellationToken)
    {
        return await _service.SyncAiSpeechAssistantDescriptionsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
