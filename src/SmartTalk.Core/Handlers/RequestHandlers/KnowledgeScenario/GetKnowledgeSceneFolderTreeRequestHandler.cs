using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneFolderTreeRequestHandler : IRequestHandler<GetKnowledgeSceneFolderTreeRequest, GetKnowledgeSceneFolderTreeResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneFolderTreeRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneFolderTreeResponse> Handle(IReceiveContext<GetKnowledgeSceneFolderTreeRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneFolderTreeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
