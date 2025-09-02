using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosCompanyWithStoresRequestHandler : IRequestHandler<GetCompanyWithStoresRequest, GetPosCompanyWithStoresResponse>
{
    private readonly IPosService _posService;

    public GetPosCompanyWithStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosCompanyWithStoresResponse> Handle(IReceiveContext<GetCompanyWithStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosCompanyWithStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}