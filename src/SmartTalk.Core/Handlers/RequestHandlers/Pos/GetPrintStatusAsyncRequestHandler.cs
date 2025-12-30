using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPrintStatusAsyncRequestHandler : IRequestHandler<GetPrintStatusRequest, GetPrintStatusResponse>
{
    private readonly IPosService _posService;

    public GetPrintStatusAsyncRequestHandler(IPosService posService)
    {
        _posService = posService;
    }
    
    public async Task<GetPrintStatusResponse> Handle(IReceiveContext<GetPrintStatusRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPrintStatusAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}