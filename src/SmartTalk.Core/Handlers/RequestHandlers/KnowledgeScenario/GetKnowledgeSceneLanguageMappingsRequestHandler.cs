using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneLanguageMappingsRequestHandler : IRequestHandler<GetKnowledgeSceneLanguageMappingsRequest, GetKnowledgeSceneLanguageMappingsResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneLanguageMappingsRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneLanguageMappingsResponse> Handle(IReceiveContext<GetKnowledgeSceneLanguageMappingsRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneLanguageMappingsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
