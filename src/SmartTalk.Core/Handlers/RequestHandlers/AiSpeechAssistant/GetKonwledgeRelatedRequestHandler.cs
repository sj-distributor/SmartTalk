using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetKonwledgeRelatedRequestHandler : IRequestHandler<GetKonwledgeRelatedRequest, GetKonwledgeRelatedResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public GetKonwledgeRelatedRequestHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<GetKonwledgeRelatedResponse> Handle(IReceiveContext<GetKonwledgeRelatedRequest> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.GetKonwledgeRelatedAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}