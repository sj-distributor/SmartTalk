using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetAssistantNumberRequestHandler : IRequestHandler<GetAssistantNumberRequest, GetAssistantNumberResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public GetAssistantNumberRequestHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    public async Task<GetAssistantNumberResponse> Handle(IReceiveContext<GetAssistantNumberRequest> context, CancellationToken cancellationToken)
    {
        return await _assistantService.GetAssistantNumberAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}