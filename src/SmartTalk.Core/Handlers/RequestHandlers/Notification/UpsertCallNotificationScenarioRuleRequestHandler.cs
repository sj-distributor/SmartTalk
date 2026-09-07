using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Notification;
using SmartTalk.Messages.Requests.Notification;

namespace SmartTalk.Core.Handlers.RequestHandlers.Notification;

public class UpsertCallNotificationScenarioRuleRequestHandler : IRequestHandler<UpsertCallNotificationScenarioRuleRequest, CallNotificationOperationResponse>
{
    private readonly ICallNotificationService _service;
    
    public UpsertCallNotificationScenarioRuleRequestHandler(ICallNotificationService service)
    {
        _service = service;
    }

    public async Task<CallNotificationOperationResponse> Handle(IReceiveContext<UpsertCallNotificationScenarioRuleRequest> context, CancellationToken cancellationToken)
    {
        await _service.UpsertScenarioRuleAsync(context.Message, cancellationToken).ConfigureAwait(false);
      
        return new CallNotificationOperationResponse();
    }
}