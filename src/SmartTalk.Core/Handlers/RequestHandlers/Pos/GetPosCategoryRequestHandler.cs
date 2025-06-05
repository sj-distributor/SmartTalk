using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosCategoryRequestHandler : IRequestHandler<GetPosCategoryRequest, GetPosCategoryResponse>
{
    private readonly IPosService _service;

    public GetPosCategoryRequestHandler(IPosService service)
    {
        _service = service;
    }

    public async Task<GetPosCategoryResponse> Handle(IReceiveContext<GetPosCategoryRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetPosCategoryAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}