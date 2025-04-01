using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.RequestHandlers.AiSpeechAssistant;

public class GetNumbersRequestHandler : IRequestHandler<GetNumbersRequest, GetNumbersResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public GetNumbersRequestHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    public async Task<GetNumbersResponse> Handle(IReceiveContext<GetNumbersRequest> context, CancellationToken cancellationToken)
    {
        return await _assistantService.GetNumbersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}