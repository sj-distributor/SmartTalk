using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetKonwledgesRequestHandler : IRequestHandler<GetKonwledgesRequest, GetKonwledgesResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public GetKonwledgesRequestHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<GetKonwledgesResponse> Handle(IReceiveContext<GetKonwledgesRequest> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.GetKonwledgesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}