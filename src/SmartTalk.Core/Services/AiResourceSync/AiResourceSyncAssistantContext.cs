using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public class AiResourceSyncAssistantContext
{
    public List<CrmAutoSyncAssistantLocationDto> ExistingCrmAssistants { get; set; }

    public IReadOnlyDictionary<int, CrmAutoSyncAssistantLocationDto> ExistingCrmAssistantsById { get; set; }
    
    public Dictionary<string, CrmAutoSyncAssistantLocationDto> ExistingCrmAssistantsByName { get; set; }

    public Dictionary<int, HashSet<string>> AssistantCustomerIdsByAssistantId { get; set; }

    public Dictionary<string, HashSet<int>> AssistantIdsByCustomerId { get; set; }

    public HashSet<int> ClaimedAssistantIds { get; set; }

    public Dictionary<string, Agent> SalesAgentCache { get; set; }

    public Dictionary<string, Domain.AISpeechAssistant.AiSpeechAssistant> CustomerKnowledgeAssistantCache { get; set; }
}
