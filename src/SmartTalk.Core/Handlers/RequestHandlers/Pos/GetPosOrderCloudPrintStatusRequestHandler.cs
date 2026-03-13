using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosOrderCloudPrintStatusRequestHandler : IRequestHandler<GetPosOrderCloudPrintStatusRequest, GetPosOrderCloudPrintStatusResponse>
{
    private readonly IPosService _posService;

    public GetPosOrderCloudPrintStatusRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetPosOrderCloudPrintStatusResponse> Handle(IReceiveContext<GetPosOrderCloudPrintStatusRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosOrderCloudPrintStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}