using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantInboundRoutesRequestHandler : IRequestHandler<GetAiSpeechAssistantInboundRoutesRequest, GetAiSpeechAssistantInboundRoutesResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public GetAiSpeechAssistantInboundRoutesRequestHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<GetAiSpeechAssistantInboundRoutesResponse> Handle(IReceiveContext<GetAiSpeechAssistantInboundRoutesRequest> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.GetAiSpeechAssistantInboundRoutesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}