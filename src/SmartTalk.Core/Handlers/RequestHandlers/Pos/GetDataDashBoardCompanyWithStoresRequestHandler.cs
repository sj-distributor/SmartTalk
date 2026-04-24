using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetDataDashBoardCompanyWithStoresRequestHandler : IRequestHandler<GetDataDashBoardCompanyWithStoresRequest, GetDataDashBoardCompanyWithStoresResponse>
{
    private readonly IPosService _posService;

    public GetDataDashBoardCompanyWithStoresRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetDataDashBoardCompanyWithStoresResponse> Handle(IReceiveContext<GetDataDashBoardCompanyWithStoresRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetDataDashBoardCompanyWithStoresAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}