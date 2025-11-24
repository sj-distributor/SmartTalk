namespace SmartTalk.Messages.Dto.Pos;

public class SimpleStructuredStoreDto
{
    public int StoreId { get; set; }
    
    public List<SimpleStoreAgentDto> SimpleStoreAgents { get; set; }
    
    public int UnreviewTotalCount => SimpleStoreAgents.Sum(x => x.UnreviewCount);
}

public class SimpleStoreAgentDto
{
    public int StoreId { get; set; }
    
    public int AgentId { get; set; }
    
    public int UnreviewCount { get; set; }
}