using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class CheckPosCompanyRequestHandler : IRequestHandler<CheckPosCompanyRequest, CheckPosCompanyResponse>
{
    private readonly IPosService _posService;

    public CheckPosCompanyRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<CheckPosCompanyResponse> Handle(IReceiveContext<CheckPosCompanyRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.CheckPosCompanyAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}