using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneKnowledgesRequestHandler : IRequestHandler<GetKnowledgeSceneKnowledgesRequest, GetKnowledgeSceneKnowledgesResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneKnowledgesRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneKnowledgesResponse> Handle(IReceiveContext<GetKnowledgeSceneKnowledgesRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneKnowledgesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
