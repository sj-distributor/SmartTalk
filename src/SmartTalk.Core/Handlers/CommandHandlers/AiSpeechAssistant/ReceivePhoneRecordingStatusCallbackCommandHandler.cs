using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class ReceivePhoneRecordingStatusCallbackCommandHandler : ICommandHandler<ReceivePhoneRecordingStatusCallbackCommand>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public ReceivePhoneRecordingStatusCallbackCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task Handle(IReceiveContext<ReceivePhoneRecordingStatusCallbackCommand> context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}