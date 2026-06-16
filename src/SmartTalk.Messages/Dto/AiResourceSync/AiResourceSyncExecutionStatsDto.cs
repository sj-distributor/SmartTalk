using SmartTalk.Messages.Dto.SalesAutoCreate;

namespace SmartTalk.Messages.Dto.AiResourceSync;

public class AiResourceSyncExecutionStatsDto
{
    public int TotalCount { get; set; }
    
    public int CreatedStoreCount { get; set; }
    
    public int CreatedAgentCount { get; set; }
    
    public int CreatedAssistantCount { get; set; }
    
    public int CreatedKnowledgeCount { get; set; }
    
    public int AppliedSceneCount { get; set; }
    
    public int TransferredAssistantCount { get; set; } 
    
    public int DeactivatedAssistantCount { get; set; }
    
    public List<AiResourceSyncCreateStoreChangeDto> CreatedStores { get; } = new();
    
    public List<AiResourceSyncCreateAgentChangeDto> CreatedAgents { get; } = new();
    
    public List<AiResourceSyncCreateAssistantChangeDto> CreatedAssistants { get; } = new();
    
    public List<AiResourceSyncCreateAssistantChangeDto> TransferredAssistants { get; } = new();
    
    public List<AiResourceSyncCreateAssistantChangeDto> RenamedAssistants { get; } = new();
    
    public List<AiResourceSyncCreateAssistantChangeDto> DeactivatedAssistants { get; } = new();
    
    public List<string> Warnings { get; } = new();
}