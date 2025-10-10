using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosMenusListRequestHandler : IRequestHandler<GetPosMenusListRequest, GetPosMenusListResponse>
{
    private readonly IPosService _service;

    public GetPosMenusListRequestHandler(IPosService service)
    {
        _service = service;
    }
    
    public async Task<GetPosMenusListResponse> Handle(IReceiveContext<GetPosMenusListRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetPosMenusListAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}