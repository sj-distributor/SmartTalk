using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneCompaniesRequestHandler : IRequestHandler<GetKnowledgeSceneCompaniesRequest, GetKnowledgeSceneCompaniesResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneCompaniesRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneCompaniesResponse> Handle(IReceiveContext<GetKnowledgeSceneCompaniesRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneCompaniesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
