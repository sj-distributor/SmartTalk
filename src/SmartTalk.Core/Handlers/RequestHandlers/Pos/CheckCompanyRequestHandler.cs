using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class CheckCompanyRequestHandler : IRequestHandler<CheckCompanyOrStoreRequest, CheckCompanyOrStoreResponse>
{
    private readonly IPosService _posService;

    public CheckCompanyRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<CheckCompanyOrStoreResponse> Handle(IReceiveContext<CheckCompanyOrStoreRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.CheckPosCompanyOrStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}