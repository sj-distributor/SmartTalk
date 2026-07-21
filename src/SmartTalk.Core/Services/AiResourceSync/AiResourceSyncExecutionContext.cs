using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.AiResourceSync;

public class SalesKnowledgeSyncTask
{
    public string StoreName { get; set; }

    public CrmSalesAutoSyncCustomerDto SeedCustomer { get; set; }

    public CrmSalesAutoSyncCustomerGroup MergedGroup { get; set; }
}

public class AiResourceSyncStoreContext
{
    public Dictionary<string, CompanyStore> StoreMap { get; set; }

    public IReadOnlyDictionary<int, string> ExistingStoreNamesById { get; set; }
}

public class SourceSceneLookup
{
    public Dictionary<AutoAddLanguage, KnowledgeScene> MappingScenes { get; set; }

    public Dictionary<int, List<KnowledgeSceneItem>> SceneItems { get; set; }
}

public class AiResourceSyncExecutionContext
{
    public AiResourceSyncStoreContext StoreContext { get; set; }

    public AiResourceSyncAssistantContext AssistantContext { get; set; }

    public SourceSceneLookup SourceSceneLookup { get; set; }

    public List<SalesKnowledgeSyncTask> SyncTasks { get; set; }
}
