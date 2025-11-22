using SmartTalk.Messages.Dto.Agent;

namespace SmartTalk.Messages.Dto.Pos;

public class StoreAgentsDto
{
    public List<StructuredStoreDto> Stores { get; set; }

    public int StoreUnreviewTotalCount => Stores.Sum(x => x.Agents.Sum(k => k.UnreviewCount));
}

public class StructuredStoreDto
{
    public CompanyStoreDto Store { get; set; }

    public List<AgentDto> Agents {get; set; }

    public int AgentUnreviewTotalCount => Agents.Sum(x => x.UnreviewCount);
}