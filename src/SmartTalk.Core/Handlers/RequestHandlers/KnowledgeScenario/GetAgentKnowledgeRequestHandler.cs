using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetAgentKnowledgeRequestHandler : IRequestHandler<GetAgentKnowledgeRequest, GetAgentKnowledgeResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetAgentKnowledgeRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }
    
    public async Task<GetAgentKnowledgeResponse> Handle(IReceiveContext<GetAgentKnowledgeRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetAgentKnowledgeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
