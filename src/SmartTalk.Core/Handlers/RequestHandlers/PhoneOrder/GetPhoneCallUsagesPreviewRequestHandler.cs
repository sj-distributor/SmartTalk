using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Handlers.RequestHandlers.PhoneOrder;

public class GetPhoneCallUsagesPreviewRequestHandler : IRequestHandler<GetPhoneCallUsagesPreviewRequest, GetPhoneCallUsagesPreviewResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;

    public GetPhoneCallUsagesPreviewRequestHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<GetPhoneCallUsagesPreviewResponse> Handle(IReceiveContext<GetPhoneCallUsagesPreviewRequest> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.GetPhoneCallUsagesPreviewAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}