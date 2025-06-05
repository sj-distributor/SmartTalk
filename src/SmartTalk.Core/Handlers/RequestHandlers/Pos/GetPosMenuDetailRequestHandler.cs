using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosMenuDetailRequestHandler : IRequestHandler<GetPosMenuDetailRequest, GetPosMenuDetailResponse>
{
    private readonly IPosService _service;

    public GetPosMenuDetailRequestHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<GetPosMenuDetailResponse> Handle(IReceiveContext<GetPosMenuDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetPosMenuDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}