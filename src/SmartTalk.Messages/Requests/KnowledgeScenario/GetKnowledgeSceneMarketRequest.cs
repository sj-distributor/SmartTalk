using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetKnowledgeSceneMarketRequest : IRequest
{
    public int CompanyId { get; set; }

    public int StoreId { get; set; }

    public KnowledgeSceneMarketType MarketType { get; set; }

    public string Keyword { get; set; }
}

public class GetKnowledgeSceneMarketResponse : SmartTalkResponse<GetKnowledgeSceneMarketResponseData>
{
}

public class GetKnowledgeSceneMarketResponseData
{
    public List<KnowledgeSceneDto> Scenes { get; set; }
}
