using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetDataDashBoardOptionsRequestHandler : IRequestHandler<GetDataDashBoardOptionsRequest, GetDataDashBoardOptionsResponse>
{
    private readonly IPosService _posService;

    public GetDataDashBoardOptionsRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetDataDashBoardOptionsResponse> Handle(IReceiveContext<GetDataDashBoardOptionsRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetDataDashBoardOptionsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
