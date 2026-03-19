using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.DynamicInterface;
using SmartTalk.Messages.Requests.DynamicInterface;

namespace SmartTalk.Core.Handlers.RequestHandlers.DynamicInterface;

public class GetDynamicInterfaceTreeHandler : IRequestHandler<GetDynamicInterfaceTreeRequest, GetDynamicInterfaceTreeResponse>
{
    private readonly IDynamicInterfaceService _dynamicInterfaceService;

    public GetDynamicInterfaceTreeHandler(IDynamicInterfaceService dynamicInterfaceService)
    {
        _dynamicInterfaceService = dynamicInterfaceService;
    }

    public async Task<GetDynamicInterfaceTreeResponse> Handle(IReceiveContext<GetDynamicInterfaceTreeRequest> context, CancellationToken cancellationToken)
    {
        return await _dynamicInterfaceService.GetDynamicInterfaceTreeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}