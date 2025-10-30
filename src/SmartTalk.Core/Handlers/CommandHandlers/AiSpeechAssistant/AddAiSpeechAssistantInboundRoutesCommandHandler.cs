using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class AddAiSpeechAssistantInboundRoutesCommandHandler : ICommandHandler<AddAiSpeechAssistantInboundRoutesCommand, AddAiSpeechAssistantInboundRoutesResponse>
{
    private readonly IAiSpeechAssistantService _aiiSpeechAssistantService;

    public AddAiSpeechAssistantInboundRoutesCommandHandler(IAiSpeechAssistantService aiiSpeechAssistantService)
    {
        _aiiSpeechAssistantService = aiiSpeechAssistantService;
    }

    public async Task<AddAiSpeechAssistantInboundRoutesResponse> Handle(IReceiveContext<AddAiSpeechAssistantInboundRoutesCommand> context, CancellationToken cancellationToken)
    {
        return await _aiiSpeechAssistantService.AddAiSpeechAssistantInboundRoutesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}