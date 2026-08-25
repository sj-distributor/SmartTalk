using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneHistoryRequest : IRequest
{
    public int SceneId { get; set; }

    public int? PageIndex { get; set; } = 1;

    public int? PageSize { get; set; } = 15;
}

public class GetKnowledgeSceneHistoryResponse : SmartTalkResponse<GetKnowledgeSceneHistoryResponseData>
{
}

public class GetKnowledgeSceneHistoryResponseData
{
    public int Count { get; set; }

    public List<KnowledgeSceneHistoryDto> Scenes { get; set; }
}
