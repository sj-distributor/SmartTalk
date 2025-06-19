using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantSessionRequestHandler : IRequestHandler<GetAiSpeechAssistantSessionRequest, GetAiSpeechAssistantSessionResponse>
{
    private readonly IAiSpeechAssistantService _speechAssistantService;

    public GetAiSpeechAssistantSessionRequestHandler(IAiSpeechAssistantService speechAssistantService)
    {
        _speechAssistantService = speechAssistantService;
    }

    public async Task<GetAiSpeechAssistantSessionResponse> Handle(IReceiveContext<GetAiSpeechAssistantSessionRequest> context, CancellationToken cancellationToken)
    {
        return await _speechAssistantService.GetAiSpeechAssistantSessionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}