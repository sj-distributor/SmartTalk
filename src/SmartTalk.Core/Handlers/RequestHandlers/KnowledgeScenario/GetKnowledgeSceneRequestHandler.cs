using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneRequestHandler : IRequestHandler<GetKnowledgeSceneRequest, GetKnowledgeSceneResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneResponse> Handle(IReceiveContext<GetKnowledgeSceneRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
