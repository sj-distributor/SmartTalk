using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Handlers.RequestHandlers.KnowledgeScenario;

public class GetKnowledgeScenesRequestHandler : IRequestHandler<GetKnowledgeScenesRequest, GetKnowledgeScenesResponse>
{
    private readonly IKnowledgeScenarioService _knowledgeScenarioService;

    public GetKnowledgeScenesRequestHandler(IKnowledgeScenarioService knowledgeScenarioService)
    {
        _knowledgeScenarioService = knowledgeScenarioService;
    }

    public async Task<GetKnowledgeScenesResponse> Handle(IReceiveContext<GetKnowledgeScenesRequest> context, CancellationToken cancellationToken)
    {
        return await _knowledgeScenarioService.GetKnowledgeScenesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
