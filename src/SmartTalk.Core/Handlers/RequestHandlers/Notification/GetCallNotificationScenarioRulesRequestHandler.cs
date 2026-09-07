using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Notification;
using SmartTalk.Messages.Requests.Notification;

namespace SmartTalk.Core.Handlers.RequestHandlers.Notification;

public class GetCallNotificationScenarioRulesRequestHandler : IRequestHandler<GetCallNotificationScenarioRulesRequest, CallNotificationScenarioRulesResponse>
{
    private readonly ICallNotificationService _service;
    
    public GetCallNotificationScenarioRulesRequestHandler(ICallNotificationService service)
    {
        _service = service;
    }

    public async Task<CallNotificationScenarioRulesResponse> Handle(IReceiveContext<GetCallNotificationScenarioRulesRequest> context, CancellationToken cancellationToken)
    {
        var callNotificationScenarioRuleDtos = await _service.GetScenarioRulesAsync(context.Message.StoreId, cancellationToken).ConfigureAwait(false);

        return new CallNotificationScenarioRulesResponse()
        {
            Data = callNotificationScenarioRuleDtos
        };
    }
}
