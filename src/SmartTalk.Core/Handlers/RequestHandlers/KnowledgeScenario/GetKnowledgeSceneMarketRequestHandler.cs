using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneMarketRequestHandler : IRequestHandler<GetKnowledgeSceneMarketRequest, GetKnowledgeSceneMarketResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneMarketRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneMarketResponse> Handle(IReceiveContext<GetKnowledgeSceneMarketRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneMarketAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
