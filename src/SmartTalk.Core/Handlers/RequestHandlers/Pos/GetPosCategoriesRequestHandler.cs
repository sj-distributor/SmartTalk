using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Handlers.RequestHandlers.Pos;

public class GetPosCategoriesRequestHandler : IRequestHandler<GetPosCategoriesRequest, GetPosCategoriesResponse>
{
    private readonly IPosService _posService;

    public GetPosCategoriesRequestHandler(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<GetPosCategoriesResponse> Handle(IReceiveContext<GetPosCategoriesRequest> context, CancellationToken cancellationToken)
    {
        return await _posService.GetPosCategoriesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}