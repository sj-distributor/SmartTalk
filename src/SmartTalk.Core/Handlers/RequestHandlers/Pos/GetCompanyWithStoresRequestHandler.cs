using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetCompanyWithStoresRequestHandler : IRequestHandler<GetCompanyWithStoresRequest, GetCompanyWithStoresResponse>
{
    private readonly IPosService _posService;

    public GetCompanyWithStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetCompanyWithStoresResponse> Handle(IReceiveContext<GetCompanyWithStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetCompanyWithStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}