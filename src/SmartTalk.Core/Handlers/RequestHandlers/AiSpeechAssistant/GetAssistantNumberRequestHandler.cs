using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAssistantNumberRequestHandler : IRequestHandler<GetAiSpeechAssistantByIdRequest, GetAiSpeechAssistantByIdResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public GetAssistantNumberRequestHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    public async Task<GetAiSpeechAssistantByIdResponse> Handle(IReceiveContext<GetAiSpeechAssistantByIdRequest> context, CancellationToken cancellationToken)
    {
        return await _assistantService.GetAiSpeechAssistantByIdAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}