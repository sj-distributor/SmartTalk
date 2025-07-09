using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class CheckPosCompanyRequestHandler : IRequestHandler<CheckPosCompanyOrStoreRequest, CheckPosCompanyOrStoreResponse>
{
    private readonly IPosService _posService;

    public CheckPosCompanyRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<CheckPosCompanyOrStoreResponse> Handle(IReceiveContext<CheckPosCompanyOrStoreRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.CheckPosCompanyOrStoreAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}