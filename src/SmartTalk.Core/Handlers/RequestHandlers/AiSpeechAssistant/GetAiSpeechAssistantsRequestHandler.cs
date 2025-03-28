using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAiSpeechAssistantsRequestHandler : IRequestHandler<GetAiSpeechAssistantsRequest, GetAiSpeechAssistantsResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public GetAiSpeechAssistantsRequestHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }
    
    public async Task<GetAiSpeechAssistantsResponse> Handle(IReceiveContext<GetAiSpeechAssistantsRequest> context, CancellationToken cancellationToken)
    {
        return await _assistantService.GetAiSpeechAssistantsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}