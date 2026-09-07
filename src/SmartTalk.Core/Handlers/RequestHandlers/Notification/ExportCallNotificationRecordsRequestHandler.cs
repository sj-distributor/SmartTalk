using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Notification;
using SmartTalk.Messages.Requests.Notification;

namespace SmartTalk.Core.Handlers.RequestHandlers.Notification;

public class ExportCallNotificationRecordsRequestHandler : IRequestHandler<ExportCallNotificationRecordsRequest, CallNotificationExportResponse>
{
    private readonly ICallNotificationService _service;
    
    public ExportCallNotificationRecordsRequestHandler(ICallNotificationService service) => _service = service;

    public async Task<CallNotificationExportResponse> Handle(
        IReceiveContext<ExportCallNotificationRecordsRequest> context, CancellationToken cancellationToken) =>
        new() { Data = await _service.ExportRecordsAsync(context.Message, cancellationToken).ConfigureAwait(false) };
}
