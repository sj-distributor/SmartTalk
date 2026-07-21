using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public class AiResourceSyncAssistantContext
{
    // 当前公司下已加载出来的全部 CRM 自动同步助理位置数据；这是后续匹配的原始集合。
    public List<CrmAutoSyncAssistantLocationDto> ExistingCrmAssistants { get; set; }
    
    // 通过 AssistantId 快速找到助理位置数据，避免频繁在列表里遍历。
    public IReadOnlyDictionary<int, CrmAutoSyncAssistantLocationDto> ExistingCrmAssistantsById { get; set; }
    
    // 通过助理名称快速找到助理；主要用于“名称完全命中”的优先匹配。
    public Dictionary<string, CrmAutoSyncAssistantLocationDto> ExistingCrmAssistantsByName { get; set; }
    
    // 记录“一个助理当前对应哪些 customerId”，用于判断是否精确匹配、超集、子集、是否需要拆分。
    public Dictionary<int, HashSet<string>> AssistantCustomerIdsByAssistantId { get; set; }
    
    // 上面那张表的反向索引：从 customerId 反查哪些助理包含它，方便快速缩小候选助理集合。
    public Dictionary<string, HashSet<int>> AssistantIdsByCustomerId { get; set; }
    
    // 本次同步过程中已经被某个客户组认领过的助理；避免同一个助理被重复分配给多个目标组。
    public HashSet<int> ClaimedAssistantIds { get; set; }
    
    // 门店 + 销售员名称级别的本地缓存，避免同一次同步里重复查询或重复创建销售员 Agent。
    public Dictionary<string, Agent> SalesAgentCache { get; set; }
    
    // 门店 + 助理名称级别的本地缓存，避免同一次同步里重复查询或重复创建客户知识助理。
    public Dictionary<string, Domain.AISpeechAssistant.AiSpeechAssistant> CustomerKnowledgeAssistantCache { get; set; }
}
