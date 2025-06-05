using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosCompanyDetailRequestHandler : IRequestHandler<GetPosCompanyDetailRequest, GetPosCompanyDetailResponse>
{
    private readonly IPosService _service;

    public GetPosCompanyDetailRequestHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<GetPosCompanyDetailResponse> Handle(IReceiveContext<GetPosCompanyDetailRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetPosCompanyDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}