using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeScenesRequest : IRequest
{
    public int FolderId { get; set; }

    public string Keyword { get; set; }
}

public class GetKnowledgeScenesResponse : SmartTalkResponse<List<KnowledgeSceneDto>>
{
}
