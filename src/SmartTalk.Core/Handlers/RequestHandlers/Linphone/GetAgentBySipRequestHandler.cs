using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Requests.Linphone;

namespace SmartTalk.Core.Handlers.RequestHandlers.Linphone;

public class GetAgentBySipRequestHandler : IRequestHandler<GetAgentBySipRequest, GetAgentBySipResponse>
{
    public ILinphoneService _LinphoneService { get; set; }
    
    public GetAgentBySipRequestHandler(ILinphoneService linphoneService)
    {
        _LinphoneService = linphoneService;
    }
    
    public async Task<GetAgentBySipResponse> Handle(IReceiveContext<GetAgentBySipRequest> context, CancellationToken cancellationToken)
    {
        return await _LinphoneService.GetAgentBySipAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}