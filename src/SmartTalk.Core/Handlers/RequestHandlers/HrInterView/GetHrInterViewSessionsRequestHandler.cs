using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;

namespace SmartTalk.Core.Handlers.RequestHandlers.HrInterView;

public class GetHrInterViewSessionsRequestHandler: IRequestHandler<GetHrInterViewSessionsRequest, GetHrInterViewSessionsResponse>
{
    private readonly IHrInterViewService _hrInterViewService;

    public GetHrInterViewSessionsRequestHandler(IHrInterViewService hrInterViewService)
    {
        _hrInterViewService = hrInterViewService;
    }

    public async Task<GetHrInterViewSessionsResponse> Handle(IReceiveContext<GetHrInterViewSessionsRequest> context, CancellationToken cancellationToken)
    {
        return await _hrInterViewService.GetHrInterViewSessionsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}