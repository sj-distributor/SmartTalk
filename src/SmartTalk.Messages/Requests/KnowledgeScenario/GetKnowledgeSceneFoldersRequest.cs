using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneFoldersRequest : IRequest
{
    public string Keyword { get; set; }
}

public class GetKnowledgeSceneFoldersResponse : SmartTalkResponse<List<KnowledgeSceneFolderDto>>
{
}
