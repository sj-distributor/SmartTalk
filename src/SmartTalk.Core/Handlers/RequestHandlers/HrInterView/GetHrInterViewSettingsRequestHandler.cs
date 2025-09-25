using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;

namespace SmartTalk.Core.Handlers.RequestHandlers.HrInterView;

public class GetHrInterViewSettingsRequestHandler: IRequestHandler<GetHrInterViewSettingsRequest, GetHrInterViewSettingsResponse>
{
    private readonly IHrInterViewService _hrInterViewService;

    public GetHrInterViewSettingsRequestHandler(IHrInterViewService hrInterViewService)
    {
        _hrInterViewService = hrInterViewService;
    }

    public async Task<GetHrInterViewSettingsResponse> Handle(IReceiveContext<GetHrInterViewSettingsRequest> context, CancellationToken cancellationToken)
    {
        return await _hrInterViewService.GetHrInterViewSettingsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}