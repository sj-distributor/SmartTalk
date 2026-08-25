using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneRelatedKnowledgesRequestHandler : IRequestHandler<GetKnowledgeSceneRelatedKnowledgesRequest, GetKnowledgeSceneRelatedKnowledgesResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneRelatedKnowledgesRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneRelatedKnowledgesResponse> Handle(IReceiveContext<GetKnowledgeSceneRelatedKnowledgesRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneRelatedKnowledgesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
