using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosMenuPreviewRequestHandler : IRequestHandler<GetPosMenuPreviewRequest, GetPosMenuPreviewResponse>
{
    private readonly IPosService _service;

    public GetPosMenuPreviewRequestHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<GetPosMenuPreviewResponse> Handle(IReceiveContext<GetPosMenuPreviewRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetPosMenuPreviewAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}