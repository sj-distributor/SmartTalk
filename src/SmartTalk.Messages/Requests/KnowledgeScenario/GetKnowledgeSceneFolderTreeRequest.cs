using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneFolderTreeRequest : IRequest
{
}

public class GetKnowledgeSceneFolderTreeResponse : SmartTalkResponse<List<KnowledgeSceneFolderTreeDto>>
{
}
