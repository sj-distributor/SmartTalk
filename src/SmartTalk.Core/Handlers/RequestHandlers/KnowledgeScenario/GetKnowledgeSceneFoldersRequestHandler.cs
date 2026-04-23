using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeSceneFoldersRequestHandler : IRequestHandler<GetKnowledgeSceneFoldersRequest, GetKnowledgeSceneFoldersResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeSceneFoldersRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeSceneFoldersResponse> Handle(IReceiveContext<GetKnowledgeSceneFoldersRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeSceneFoldersAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
