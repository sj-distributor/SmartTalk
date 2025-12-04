using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Hr;
using SmartTalk.Messages.Requests.Hr;

namespace SmartTalk.Core.Handlers.RequestHandlers.Hr;

public class GetCurrentInterviewQuestionsRequestHandler : IRequestHandler<GetCurrentInterviewQuestionsRequest, GetCurrentInterviewQuestionsResponse>
{
    private readonly IHrService _hrService;

    public GetCurrentInterviewQuestionsRequestHandler(IHrService hrService)
    {
        _hrService = hrService;
    }

    public async Task<GetCurrentInterviewQuestionsResponse> Handle(IReceiveContext<GetCurrentInterviewQuestionsRequest> context, CancellationToken cancellationToken)
    {
        return await _hrService.GetCurrentInterviewQuestionsAsync(context.Message, cancellationToken);
    }
}