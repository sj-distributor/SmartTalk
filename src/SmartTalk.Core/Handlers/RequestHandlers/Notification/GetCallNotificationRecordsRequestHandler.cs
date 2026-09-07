using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Notification;
using SmartTalk.Messages.Requests.Notification;

namespace SmartTalk.Core.Handlers.RequestHandlers.Notification;

public class GetCallNotificationRecordsRequestHandler : IRequestHandler<GetCallNotificationRecordsRequest, CallNotificationRecordsResponse>
{
    private readonly ICallNotificationService _service;
    
    public GetCallNotificationRecordsRequestHandler(ICallNotificationService service)
    {
        _service = service;
    }

    public async Task<CallNotificationRecordsResponse> Handle(IReceiveContext<GetCallNotificationRecordsRequest> context, CancellationToken cancellationToken)
    {
        var (count, records) = await _service.GetRecordsAsync(context.Message, cancellationToken).ConfigureAwait(false);

        return new CallNotificationRecordsResponse
        {
            Data = new CallNotificationRecordsResponseData
            {
                Count = count, Records = records
            }
        };
    }
}