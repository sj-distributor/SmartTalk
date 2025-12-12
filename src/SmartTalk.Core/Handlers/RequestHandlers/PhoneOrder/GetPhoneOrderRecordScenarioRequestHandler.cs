using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneOrderRecordScenarioRequestHandler : IRequestHandler<GetPhoneOrderRecordScenarioRequest, GetPhoneOrderRecordScenarioResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneOrderRecordScenarioRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<GetPhoneOrderRecordScenarioResponse> Handle(IReceiveContext<GetPhoneOrderRecordScenarioRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneOrderRecordScenarioAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}