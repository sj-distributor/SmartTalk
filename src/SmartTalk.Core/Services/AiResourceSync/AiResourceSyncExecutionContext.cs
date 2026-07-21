using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.AiResourceSync;

// 单个客户组对应的同步任务；同一个销售门店下的任务会被顺序处理。
public class SalesKnowledgeSyncTask
{
    // 当前客户组最终应该归属到哪个门店（销售 key）。
    public string StoreName { get; set; }

    // 该门店分片里挑出来的一个种子客户，主要用于创建门店时补充说明信息。
    public CrmSalesAutoSyncCustomerDto SeedCustomer { get; set; }

    // 这个任务真正要处理的“归并后客户组”。
    public CrmSalesAutoSyncCustomerGroup MergedGroup { get; set; }
}

// 门店相关的预加载数据，避免同步过程中重复解析门店名称和查询门店。
public class AiResourceSyncStoreContext
{
    // 销售 key / 门店名 -> 门店实体。
    // 后续按客户组处理时，可以直接通过目标门店名拿到对应门店。
    public Dictionary<string, CompanyStore> StoreMap { get; set; }

    // StoreId -> 门店名。
    // 主要给一些“手里只有 StoreId，但后续还想反推出原门店名”的逻辑使用。
    public IReadOnlyDictionary<int, string> ExistingStoreNamesById { get; set; }
}

// 语言到知识场景及其场景项的预加载结果。
public class SourceSceneLookup
{
    // 语言 -> 当前应使用的知识场景。
    // 例如英文客户最终应该挂哪个发布中的英文场景。
    public Dictionary<AutoAddLanguage, KnowledgeScene> MappingScenes { get; set; }

    // SceneId -> 该场景下的全部场景项。
    // 后续挂载知识时，不光要知道“选哪个场景”，也要知道这个场景里到底有什么内容。
    public Dictionary<int, List<KnowledgeSceneItem>> SceneItems { get; set; }
}

public class AiResourceSyncExecutionContext
{
    // - 门店上下文：已有门店、门店名映射
    public AiResourceSyncStoreContext StoreContext { get; set; }
    
    // - 助理上下文：已有 CRM 助理、名称索引、客户映射、缓存和已认领集合
    public AiResourceSyncAssistantContext AssistantContext { get; set; }
    
    // - 场景上下文：语言 -> 场景 -> 场景项
    public SourceSceneLookup SourceSceneLookup { get; set; }
    
    // - 待执行任务：把客户组转成按门店分片后的同步任务
    public List<SalesKnowledgeSyncTask> SyncTasks { get; set; }
}
