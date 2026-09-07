using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Notification;
using SmartTalk.Messages.Requests.Notification;

namespace SmartTalk.Core.Handlers.RequestHandlers.Notification;


public class UpdateStoreCallNotificationSettingRequestHandler : IRequestHandler<UpdateStoreCallNotificationSettingRequest, CallNotificationOperationResponse>
{
    private readonly ICallNotificationService _service;
    
    public UpdateStoreCallNotificationSettingRequestHandler(ICallNotificationService service)
    {
        _service = service;
    }
    
    public async Task<CallNotificationOperationResponse> Handle(IReceiveContext<UpdateStoreCallNotificationSettingRequest> context, CancellationToken cancellationToken)
    {
        await _service.UpdateStoreSettingAsync(context.Message, cancellationToken).ConfigureAwait(false);
        
        return new CallNotificationOperationResponse();
    }
}
