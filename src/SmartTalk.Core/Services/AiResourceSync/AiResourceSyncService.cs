using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.AiResourceSync;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Dto.SalesAutoCreate;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Events.AiResourceSync;

namespace SmartTalk.Core.Services.AiResourceSync;

public record AiResourceSyncShardExecutionResult(string SalesKey, int CustomerGroupCount, AiResourceSyncExecutionStatsDto Stats);

public record AiResourceSyncExecutionResult(int TotalCount, int ShardCount, IReadOnlyList<AiResourceSyncShardExecutionResult> Shards, AiResourceSyncExecutionStatsDto Stats, bool IsInitialRelease);

public partial interface IAiResourceSyncService : IScopedDependency
{
    Task<AiResourceSyncEvent> SyncCrmSalesAutoCreateAsync(AiResourceSyncCommand command, CancellationToken cancellationToken);
    
    Task<AiResourceSyncExecutionResult> SyncInternalAsync(AiResourceSyncCommand command, List<CrmSalesAutoSyncCustomerDto> customers, CancellationToken cancellationToken);
    
    Task RecordSyncRunAsync(AiResourceSyncCommand command, AiResourceSyncExecutionStatsDto stats, bool isInitialRelease, bool isSuccess, string errorMessage, CancellationToken cancellationToken);

    Task SendNotifyAsync(bool isSuccess, CancellationToken cancellationToken);
}

public class AiResourceSyncService : IAiResourceSyncService
{
    private const int MaxWarningEntriesToPersist = 100;
    private const int MaxDetailEntriesPerCategoryToPersist = 50;

    private record SalesKnowledgeSyncTask(string StoreName, CrmSalesAutoSyncCustomerDto SeedCustomer, CrmSalesAutoSyncCustomerGroup MergedGroup);
  
    private record SourceSceneLookup(Dictionary<AutoAddLanguage, KnowledgeScene> MappingScenes, Dictionary<int, List<KnowledgeSceneItem>> SceneItems);
 
    private readonly IMediator _mediator;
    private readonly ICrmClient _crmClient;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IKnowledgeScenarioDataProvider _knowledgeScenarioDataProvider;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly IWeChatClient _weChatClient;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly SalesSetting _salesSetting;
    private readonly AiResourceSyncSetting _aiResourceSyncSetting;

    public AiResourceSyncService(
        IMediator mediator,
        ICrmClient crmClient,
        IAgentDataProvider agentDataProvider,
        IPosDataProvider posDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IKnowledgeScenarioDataProvider knowledgeScenarioDataProvider,
        ISalesDataProvider salesDataProvider,
        IWeChatClient weChatClient,
        IRedisSafeRunner redisSafeRunner,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        SalesSetting salesSetting,
        AiResourceSyncSetting aiResourceSyncSetting)
    {
        _mediator = mediator;
        _crmClient = crmClient;
        _agentDataProvider = agentDataProvider;
        _posDataProvider = posDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
        _salesDataProvider = salesDataProvider;
        _weChatClient = weChatClient;
        _redisSafeRunner = redisSafeRunner;
        _salesSetting = salesSetting;
        _aiResourceSyncSetting = aiResourceSyncSetting;
    }

    public async Task<AiResourceSyncEvent> SyncCrmSalesAutoCreateAsync(AiResourceSyncCommand command, CancellationToken cancellationToken)
    {
        command.ServiceProviderId ??= 1;

        var (customers, totalCount) =await _crmClient.GetSalesAutoSyncCustomersAsync(isGetTotalCount: false, cancellationToken: cancellationToken).ConfigureAwait(false);
       
        Log.Information("CRM sync accepted. Total={TotalCount}, FirstPageCount={FirstPageCount}", totalCount, customers.Count);

        return new AiResourceSyncEvent
        {
            ServiceProviderId = command.ServiceProviderId.Value,
            IsManual = command.IsManual,
            InitiatedByUserId = command.InitiatedByUserId,
            TotalCount = totalCount ?? 0
        };
    }

    public async Task<AiResourceSyncExecutionResult> SyncInternalAsync(AiResourceSyncCommand command, List<CrmSalesAutoSyncCustomerDto> customers, CancellationToken cancellationToken)
    {
        if (!customers.Any())
        { 
            (customers, _) = await _crmClient.GetSalesAutoSyncCustomersAsync(isGetTotalCount: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            Log.Information("auto CRM sync start. Customers={CustomerCount}", customers.Count); 
        }
        
        var activeCustomerIds = CrmSalesAutoSyncGrouping.BuildActiveCustomerIds(customers);

        var customerGroups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);

        var customerIdLookup = CrmSalesAutoSyncGrouping.BuildCustomerIdLookup(customerGroups);
        
        var stats = new AiResourceSyncExecutionStatsDto { TotalCount = customers.Count };

        var company = await _posDataProvider.GetPosCompanyByNameAsync(_salesSetting.CompanyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
            throw new Exception($"Sales company [{_salesSetting.CompanyName}] not found.");

        var isInitialRelease = !command.IsManual
            && !await _aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(company.Id, cancellationToken).ConfigureAwait(false);

        var existingCrmAssistants = await _aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(company.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Assistants loaded. Count={AssistantCount}", existingCrmAssistants.Count);

        var existingStores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("Stores loaded. Count={StoreCount}", existingStores.Count);
        
        var claimedAssistantIds = new HashSet<int>();
        var existingCrmAssistantsByName = existingCrmAssistants
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var existingCrmAssistantsById = existingCrmAssistants
            .GroupBy(x => x.AssistantId)
            .ToDictionary(x => x.Key, x => x.First());
        var assistantCustomerIdsByAssistantId = existingCrmAssistants
            .Where(x => x.AssistantId > 0)
            .ToDictionary(
                x => x.AssistantId,
                x => TryParseAssistantIds(x.Name, out var ids)
                    ? ids
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var assistantIdsByCustomerId = BuildAssistantIdsByCustomerId(assistantCustomerIdsByAssistantId);

        var sourceSceneLookup = await BuildSourceSceneLookupAsync(company.Id, cancellationToken).ConfigureAwait(false);
        Log.Information("Scenes loaded. Count={SceneCount}", sourceSceneLookup.MappingScenes.Count);
        if (company.Name.Equals(_salesSetting.CompanyName, StringComparison.OrdinalIgnoreCase) && sourceSceneLookup.MappingScenes.Count == 0)
            throw new Exception($"Sales company [{_salesSetting.CompanyName}] has no active knowledge scene mapping.");
        
        var storeNames = customerGroups
            .Select(x => x.SalesKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var storeMap = existingStores
            .Select(x => new { Store = x, StoreName = GetStoreName(x.Names) })
            .Where(x => !string.IsNullOrWhiteSpace(x.StoreName) && storeNames.Contains(x.StoreName))
            .GroupBy(x => x.StoreName)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Store.CreatedDate).First().Store);
        
        var salesAgentCache = new Dictionary<string, Agent>(StringComparer.OrdinalIgnoreCase);
        var customerKnowledgeAssistantCache = new Dictionary<string, Core.Domain.AISpeechAssistant.AiSpeechAssistant>(StringComparer.OrdinalIgnoreCase);
        
        var syncTasks = customerGroups
            .GroupBy(x => x.SalesKey)
            .SelectMany(salesBucket =>
            {
                var seedCustomer = salesBucket.First().Customers.First();
                return salesBucket.Select(mergedGroup => new SalesKnowledgeSyncTask(salesBucket.Key, seedCustomer, mergedGroup));
            }).ToList();

        var shardResults = new List<AiResourceSyncShardExecutionResult>();

        Log.Information("Sync shards start. ShardCount={ShardCount}", syncTasks.Select(x => x.StoreName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var salesShard in syncTasks
                     .OrderBy(x => x.StoreName, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(x => x.StoreName, StringComparer.OrdinalIgnoreCase))
        {
            Log.Information("Sync shard start. StoreName={StoreName}, TaskCount={TaskCount}", salesShard.Key, salesShard.Count());
            var shardResult = await ExecuteSalesShardAsync(
                command, company.Id, salesShard.Key, salesShard.ToList(), customerIdLookup,
                storeMap, salesAgentCache, customerKnowledgeAssistantCache,
                sourceSceneLookup, existingCrmAssistants, existingCrmAssistantsById, existingCrmAssistantsByName, assistantCustomerIdsByAssistantId, assistantIdsByCustomerId, claimedAssistantIds, cancellationToken).ConfigureAwait(false);

            shardResults.Add(shardResult);
            MergeStats(stats, shardResult.Stats);
            Log.Information("Sync shard done. StoreName={StoreName}, CreatedAssistantCount={CreatedAssistantCount}, TransferredAssistantCount={TransferredAssistantCount}, DeactivatedAssistantCount={DeactivatedAssistantCount}",
                salesShard.Key, shardResult.Stats.CreatedAssistantCount, shardResult.Stats.TransferredAssistantCount, shardResult.Stats.DeactivatedAssistantCount);
        }
 
        Log.Information("Reconcile inactive assistants start. AssistantCount={AssistantCount}, ClaimedCount={ClaimedCount}", existingCrmAssistants.Count, claimedAssistantIds.Count);
        await ReconcileInactiveCustomerAssistantsAsync(activeCustomerIds, existingCrmAssistants, assistantCustomerIdsByAssistantId, claimedAssistantIds, stats, cancellationToken).ConfigureAwait(false);
        Log.Information("Reconcile inactive assistants done. DeactivatedAssistantCount={DeactivatedAssistantCount}", stats.DeactivatedAssistantCount);

        return new AiResourceSyncExecutionResult(customers.Count, shardResults.Count, shardResults, stats, isInitialRelease);
    }

    private async Task<AiResourceSyncShardExecutionResult> ExecuteSalesShardAsync(
        AiResourceSyncCommand command,
        int companyId,
        string salesKey,
        IReadOnlyList<SalesKnowledgeSyncTask> syncTasks,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup,
        Dictionary<string, CompanyStore> storeMap,
        Dictionary<string, Agent> salesAgentCache,
        Dictionary<string, Domain.AISpeechAssistant.AiSpeechAssistant> customerKnowledgeAssistantCache,
        SourceSceneLookup sourceSceneLookup,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        IReadOnlyDictionary<int, CrmAutoSyncAssistantLocationDto> existingCrmAssistantsById,
        Dictionary<string, CrmAutoSyncAssistantLocationDto> existingCrmAssistantsByName,
        Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId,
        Dictionary<string, HashSet<int>> assistantIdsByCustomerId,
        HashSet<int> claimedAssistantIds,
        CancellationToken cancellationToken)
    {
        var stats = new AiResourceSyncExecutionStatsDto();
        var seedCustomer = syncTasks.First().SeedCustomer;

        var store = await EnsureSalesStoreAsync(
            seedCustomer, companyId, storeMap, command.InitiatedByUserId, stats, cancellationToken).ConfigureAwait(false);

        var salesAgent = await EnsureSalesAgentAsync(
            command.ServiceProviderId.Value, store.Id, salesKey, salesAgentCache, stats, cancellationToken).ConfigureAwait(false);

        foreach (var syncTask in syncTasks)
        {
            await EnsureMergedCustomerKnowledgeAsync(
                command.ServiceProviderId.Value, command.InitiatedByUserId, companyId, store.Id, salesAgent.Id, syncTask.MergedGroup, customerIdLookup, storeMap,
                salesAgentCache, customerKnowledgeAssistantCache, sourceSceneLookup, existingCrmAssistants, existingCrmAssistantsById, existingCrmAssistantsByName, assistantCustomerIdsByAssistantId, assistantIdsByCustomerId, claimedAssistantIds, stats, cancellationToken).ConfigureAwait(false);
        }

        return new AiResourceSyncShardExecutionResult(salesKey, syncTasks.Count, stats);
    }

    private async Task<CompanyStore> EnsureSalesStoreAsync(
        CrmSalesAutoSyncCustomerDto customer, int companyId, Dictionary<string, CompanyStore> storeMap,
        int? initiatedByUserId, AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        var storeName = CrmSalesAutoSyncGrouping.BuildSalesKey(customer);
        if (storeMap.TryGetValue(storeName, out var store))
        {
            Log.Information("Store reuse. StoreId={StoreId}, StoreName={StoreName}", store.Id, storeName);
            return store;
        }

        Log.Information("Store create. StoreName={StoreName}", storeName);
        
        var createResponse = await _mediator.SendAsync<CreateCompanyStoreCommand, CreateCompanyStoreResponse>(new CreateCompanyStoreCommand
        {
            CompanyId = companyId,
            CreatedBy = initiatedByUserId,
            Names = BuildStoreNamesJson(storeName),
            Address = "625 VISTA WAY, MILPITAS, CA 95035",
            Description = $"Auto created from CRM sync for {customer.SalesName}",
            PhoneNumbers = new List<string> { "0123456789" },
            Latitude = "37.4249177",
            Longitude = "-121.8891812",
        }, cancellationToken).ConfigureAwait(false);
        
        stats.CreatedStoreCount++;
        store = await _posDataProvider.GetPosCompanyStoreAsync(id: createResponse.Data.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        store.IsTaskEnabled = true;
        store.Timezone = "America/Los_Angeles";
        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);
        storeMap[storeName] = store;
        RecordCreatedStore(stats, store.Id, storeName);
        
        Log.Information("Store created. StoreId={StoreId}, StoreName={StoreName}", store.Id, storeName);
        return store;
    }

    private async Task<Agent> EnsureSalesAgentAsync(
        int serviceProviderId, int storeId, string salesAgentName, Dictionary<string, Agent> salesAgentCache, 
        AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        var salesAgentCacheKey = BuildStoreScopedCacheKey(storeId, salesAgentName);
        if (salesAgentCache.TryGetValue(salesAgentCacheKey, out var cachedSalesAgent))
        {
            Log.Information("Sales agent cache hit. AgentId={AgentId}, StoreId={StoreId}, AgentName={AgentName}", cachedSalesAgent.Id, storeId, salesAgentName);
            return cachedSalesAgent;
        }

        var posAgents = await _posDataProvider.GetPosAgentsAsync(storeIds: [storeId], cancellationToken: cancellationToken).ConfigureAwait(false);
       
        Log.Information("Sales agent lookup. StoreId={StoreId}, PosAgentCount={PosAgentCount}", storeId, posAgents.Count);
        if (posAgents.Count > 0)
        {
            var existingAgents = await _agentDataProvider.GetAgentsByIdsAsync(
                posAgents.Select(x => x.AgentId).Distinct().ToList(),
                cancellationToken).ConfigureAwait(false);

            var existingSalesAgent = existingAgents
                .Where(x => x.Type == AgentType.Sales && x.SourceSystem == AgentSourceSystem.CrmAutoSync)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefault(x => string.Equals(x.Name, salesAgentName, StringComparison.OrdinalIgnoreCase))
                ?? existingAgents
                    .Where(x => x.Type == AgentType.Sales && x.SourceSystem == AgentSourceSystem.CrmAutoSync)
                    .OrderByDescending(x => x.CreatedDate)
                    .FirstOrDefault();

            if (existingSalesAgent != null)
            {
                salesAgentCache[salesAgentCacheKey] = existingSalesAgent;
                Log.Information("Sales agent reuse. AgentId={AgentId}, StoreId={StoreId}, AgentName={AgentName}", existingSalesAgent.Id, storeId, existingSalesAgent.Name);
                return existingSalesAgent;
            }
        }

        var salesAgent = await _redisSafeRunner.ExecuteWithLockAsync(
            $"crm-auto-sync:agent:{storeId}:{salesAgentName}",
            async () =>
            {
                var existing = await _agentDataProvider.GetCrmAutoSyncAgentByStoreAndNameAsync(storeId, salesAgentName, cancellationToken).ConfigureAwait(false);
                if (existing != null)
                    return existing;

                Log.Information("Sales agent create. StoreId={StoreId}, AgentName={AgentName}", storeId, salesAgentName);
                var createdSalesAgent = await _mediator.SendAsync<AddAgentCommand, AddAgentResponse>(new AddAgentCommand
                {
                    ServiceProviderId = serviceProviderId,
                    IsReceivingCall = true,
                    ServiceHours = "[{\"day\":0,\"hours\":[{\"start\":\"00:00\",\"end\":\"23:59\"}]},{\"day\":1,\"hours\":[{\"start\":\"00:00\",\"end\":\"23:59\"}]},{\"day\":2,\"hours\":[{\"start\":\"00:00\",\"end\":\"23:59\"}]},{\"day\":3,\"hours\":[{\"start\":\"00:00\",\"end\":\"23:59\"}]},{\"day\":4,\"hours\":[{\"start\":\"00:00\",\"end\":\"23:59\"}]},{\"day\":5,\"hours\":[{\"start\":\"00:00\",\"end\":\"23:59\"}]},{\"day\":6,\"hours\":[{\"start\":\"00:00\",\"end\":\"23:59\"}]}]",
                    StoreId = storeId,
                    Name = salesAgentName,
                    TransferCallNumber = "",
                    Voice = "alloy",
                    WaitInterval = 2500,
                    Brief = $"Auto created from CRM sync for {salesAgentName}",
                    AgentType = AgentType.Sales,
                    SourceSystem = AgentSourceSystem.CrmAutoSync,
                    IsDisplay = true,
                    IsSurface = true
                }, cancellationToken).ConfigureAwait(false);

                return await _agentDataProvider.GetAgentByIdAsync(createdSalesAgent.Data.Id, cancellationToken).ConfigureAwait(false);
            },
            expiry: TimeSpan.FromMinutes(2),
            wait: TimeSpan.FromSeconds(5),
            retry: TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        salesAgent ??= await _agentDataProvider.GetCrmAutoSyncAgentByStoreAndNameAsync(storeId, salesAgentName, cancellationToken).ConfigureAwait(false);
        if (salesAgent == null)
            throw new Exception($"Sales agent create failed. StoreId={storeId}, AgentName={salesAgentName}");

        salesAgentCache[salesAgentCacheKey] = salesAgent;
        stats.CreatedAgentCount++;
        RecordCreatedAgent(stats, salesAgent.Id, storeId, salesAgentName);
        
        Log.Information("Sales agent created. AgentId={AgentId}, StoreId={StoreId}, AgentName={AgentName}", salesAgent.Id, storeId, salesAgentName);
        return salesAgent;
    }

    private async Task EnsureMergedCustomerKnowledgeAsync(
        int serviceProviderId,
        int? initiatedByUserId,
        int companyId,
        int storeId,
        int salesAgentId,
        CrmSalesAutoSyncCustomerGroup mergedGroup,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup,
        Dictionary<string, CompanyStore> storeMap,
        Dictionary<string, Agent> salesAgentCache,
        Dictionary<string, Domain.AISpeechAssistant.AiSpeechAssistant> customerKnowledgeAssistantCache,
        SourceSceneLookup sourceSceneLookup,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        IReadOnlyDictionary<int, CrmAutoSyncAssistantLocationDto> existingCrmAssistantsById,
        Dictionary<string, CrmAutoSyncAssistantLocationDto> existingCrmAssistantsByName,
        Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId,
        Dictionary<string, HashSet<int>> assistantIdsByCustomerId,
        HashSet<int> claimedAssistantIds,
        AiResourceSyncExecutionStatsDto stats,
        CancellationToken cancellationToken)
    {
        var customerAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(mergedGroup.CustomerIds, mergedGroup.Language);
        
        Log.Information(
            "Assistant ensure. Name={AssistantName}, StoreId={StoreId}, AgentId={SalesAgentId}, SalesKey={SalesKey}, Customers={CustomerIds}, Lang={Language}",
            customerAssistantName, storeId, salesAgentId, mergedGroup.SalesKey, string.Join("/", mergedGroup.CustomerIds), mergedGroup.Language ?? "英文");
      
        await ResolveMergedCustomerAssistantAsync(
            serviceProviderId, initiatedByUserId, companyId, storeId, salesAgentId, customerAssistantName, mergedGroup, customerIdLookup,
            storeMap, salesAgentCache, customerKnowledgeAssistantCache, sourceSceneLookup,
            existingCrmAssistants, existingCrmAssistantsById, existingCrmAssistantsByName, assistantCustomerIdsByAssistantId, assistantIdsByCustomerId, claimedAssistantIds, stats, cancellationToken).ConfigureAwait(false);

    }

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> EnsureCustomerKnowledgeAssistantAsync(
        int serviceProviderId,
        int? initiatedByUserId,
        int salesAgentId,
        int storeId,
        string customerKnowledgeAssistantName,
        string language,
        IReadOnlyList<string> customerIds,
        SourceSceneLookup sourceSceneLookup,
        Dictionary<string, Core.Domain.AISpeechAssistant.AiSpeechAssistant> customerKnowledgeAssistantCache,
        AiResourceSyncExecutionStatsDto stats,
        CancellationToken cancellationToken)
    {
        var assistantCacheKey = BuildStoreScopedCacheKey(storeId, customerKnowledgeAssistantName);
        if (customerKnowledgeAssistantCache.TryGetValue(assistantCacheKey, out var assistant))
        {
            Log.Information("Assistant cache hit. AssistantId={AssistantId}, StoreId={StoreId}, Name={AssistantName}", assistant.Id, storeId, customerKnowledgeAssistantName);
            return assistant;
        }

        assistant = await _redisSafeRunner.ExecuteWithLockAsync(
            $"crm-auto-sync:assistant:{storeId}:{customerKnowledgeAssistantName}",
            async () =>
            {
                var existing = await _aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(storeId, customerKnowledgeAssistantName, cancellationToken).ConfigureAwait(false);
                if (existing != null)
                    return existing;

                Log.Information("Assistant create. StoreId={StoreId}, AgentId={AgentId}, Name={AssistantName}, Customers={CustomerIds}, Lang={Language}",
                    storeId, salesAgentId, customerKnowledgeAssistantName, string.Join("/", customerIds), language ?? "英文");

                return await CreateCustomerKnowledgeAssistantAsync(
                    serviceProviderId, initiatedByUserId, salesAgentId, storeId, customerKnowledgeAssistantName,
                    CrmToAutoAddLanguageConverter.NormalizeToken(language), customerIds, sourceSceneLookup,
                    customerKnowledgeAssistantCache, stats, cancellationToken).ConfigureAwait(false);
            },
            expiry: TimeSpan.FromMinutes(2),
            wait: TimeSpan.FromSeconds(5),
            retry: TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        assistant ??= await _aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(storeId, customerKnowledgeAssistantName, cancellationToken).ConfigureAwait(false);
        if (assistant == null)
            throw new Exception($"Assistant create failed. StoreId={storeId}, Name={customerKnowledgeAssistantName}");

        stats.CreatedAssistantCount++;
        RecordCreatedAssistant(stats, assistant.Id, storeId, salesAgentId, assistant.Name);
        Log.Information("Assistant created. AssistantId={AssistantId}, StoreId={StoreId}, Name={AssistantName}", assistant.Id, storeId, assistant.Name);
        return assistant;
    }

    private async Task<Domain.AISpeechAssistant.AiSpeechAssistant> CreateCustomerKnowledgeAssistantAsync(
        int serviceProviderId, int? initiatedByUserId, int salesAgentId, int storeId, string customerKnowledgeAssistantName, string language,
        IReadOnlyList<string> customerIds, SourceSceneLookup sourceSceneLookup,
        Dictionary<string, Core.Domain.AISpeechAssistant.AiSpeechAssistant> customerKnowledgeAssistantCache, AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        var assistantCacheKey = BuildStoreScopedCacheKey(storeId, customerKnowledgeAssistantName);
        var details = BuildSourceSceneKnowledgeDetails(language, customerIds, sourceSceneLookup, stats);
        Log.Information("Assistant add request. Name={AssistantName}, DetailCount={DetailCount}", customerKnowledgeAssistantName, details.Count);

        var created = await _mediator.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(new AddAiSpeechAssistantCommand
        {
            ServiceProviderId = serviceProviderId,
            AgentId = salesAgentId,
            CreatedBy = initiatedByUserId,
            AssistantName = customerKnowledgeAssistantName,
            Greetings = _aiResourceSyncSetting.DefaultAssistantGreetings,
            AgentType = AgentType.Sales,
            SourceSystem = AgentSourceSystem.CrmAutoSync,
            IsDisplay = true,
            ModelLanguage = language,
            Channels = new List<AiSpeechAssistantChannel> { AiSpeechAssistantChannel.PhoneChat },
            Details = details
        }, cancellationToken).ConfigureAwait(false);

        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(created.Data.Id, cancellationToken).ConfigureAwait(false);
        customerKnowledgeAssistantCache[assistantCacheKey] = assistant;
        stats.CreatedKnowledgeCount++;
        return assistant;
    }
    
    private List<AiSpeechAssistantKnowledgeDetailDto> BuildSourceSceneKnowledgeDetails(
        string language, IReadOnlyList<string> customerIds,
        SourceSceneLookup sourceSceneLookup, AiResourceSyncExecutionStatsDto stats)
    {
        var scene = ResolveSourceScene(sourceSceneLookup, language);
        
        if (scene == null)
        {
            Log.Warning(
                "Scene missing. Customers={CustomerIds}, Lang={Language}", string.Join("/", customerIds), language ?? "英文");
            stats.Warnings.Add($"Customer [{string.Join("/", customerIds)}] language [{language ?? "英文"}] has no available source scene mapping yet.");
            return new List<AiSpeechAssistantKnowledgeDetailDto>();
        }

        if (!sourceSceneLookup.SceneItems.TryGetValue(scene.Id, out var sceneItems))
            sceneItems = new List<KnowledgeSceneItem>();

        if (sceneItems.Count == 0)
        {
            Log.Warning("Scene empty. SceneId={SceneId}, Customers={CustomerIds}, Lang={Language}",
                scene.Id, string.Join("/", customerIds), language ?? "英文");
            stats.Warnings.Add($"Scene [{scene.Id}] has no items for customer [{string.Join("/", customerIds)}] language [{language ?? "英文"}].");
            return new List<AiSpeechAssistantKnowledgeDetailDto>();
        }

        Log.Information(
            "Scene items loaded. SceneId={SceneId}, Items={ItemCount}, Customers={CustomerIds}, Lang={Language}",
            scene.Id, sceneItems.Count, string.Join("/", customerIds), language ?? "英文");
        
        return sceneItems.Select(x => new AiSpeechAssistantKnowledgeDetailDto
        {
            KnowledgeName = x.Name,
            FormatType = MapKnowledgeSceneItemType(x.Type),
            Content = x.Content,
            FileName = x.FileName
        }).ToList();
    }

    private static AiSpeechAssistantKonwledgeFormatType MapKnowledgeSceneItemType(KnowledgeSceneItemType type)
    {
        return type switch
        {
            KnowledgeSceneItemType.FAQ => AiSpeechAssistantKonwledgeFormatType.FAQ,
            KnowledgeSceneItemType.File => AiSpeechAssistantKonwledgeFormatType.FIle,
            _ => AiSpeechAssistantKonwledgeFormatType.Text
        };
    }

    private async Task ResolveMergedCustomerAssistantAsync(
        int serviceProviderId, int? initiatedByUserId, int companyId, int targetStoreId, int salesAgentId, string assistantName, CrmSalesAutoSyncCustomerGroup mergedGroup,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup, Dictionary<string, CompanyStore> storeMap,
        Dictionary<string, Agent> salesAgentCache,
        Dictionary<string, Core.Domain.AISpeechAssistant.AiSpeechAssistant> customerKnowledgeAssistantCache, SourceSceneLookup sourceSceneLookup,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        IReadOnlyDictionary<int, CrmAutoSyncAssistantLocationDto> existingCrmAssistantsById,
        Dictionary<string, CrmAutoSyncAssistantLocationDto> existingCrmAssistantsByName,
        Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId,
        Dictionary<string, HashSet<int>> assistantIdsByCustomerId,
        HashSet<int> claimedAssistantIds, AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        Log.Information(
            "Assistant resolve. Name={AssistantName}, StoreId={TargetStoreId}, Customers={CustomerIds}, Lang={Language}",
            assistantName, targetStoreId, string.Join("/", mergedGroup.CustomerIds), mergedGroup.Language ?? "英文");

        existingCrmAssistantsByName.TryGetValue(assistantName, out var exactMatch);
        
        if (exactMatch != null)
        {
            Log.Information(
                "Assistant match: exact. AssistantId={AssistantId}, StoreId={CurrentStoreId}, TargetStoreId={TargetStoreId}",
                exactMatch.AssistantId, exactMatch.StoreId, targetStoreId);
            
            claimedAssistantIds.Add(exactMatch.AssistantId);
            if (exactMatch.StoreId != targetStoreId)
            {
                await TransferCustomerAssistantToStoreAsync(exactMatch, targetStoreId, stats, cancellationToken).ConfigureAwait(false);
                exactMatch.StoreId = targetStoreId;
                stats.TransferredAssistantCount++;
            }
            
            return;
        }
        
        var desiredIds = mergedGroup.CustomerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sameStoreMatch = FindBestMatchingAssistant(existingCrmAssistantsById, assistantCustomerIdsByAssistantId, assistantIdsByCustomerId, desiredIds, targetStoreId, claimedAssistantIds);
        if (sameStoreMatch != null)
        {
            Log.Information(
                "Assistant match: same-store. AssistantId={AssistantId}, Matched={MatchedAssistantName}, Target={TargetAssistantName}",
                sameStoreMatch.AssistantId, sameStoreMatch.Name, assistantName);
            
            if (assistantCustomerIdsByAssistantId.TryGetValue(sameStoreMatch.AssistantId, out var existingIds) && existingIds.IsSupersetOf(desiredIds) && !existingIds.SetEquals(desiredIds))
            {
                var removedCustomerIds = existingIds.Except(desiredIds, StringComparer.OrdinalIgnoreCase).ToList();
                Log.Information(
                    "Assistant split needed. AssistantId={AssistantId}, Existing={ExistingCustomerIds}, Desired={DesiredCustomerIds}, Removed={RemovedCustomerIds}",
                    sameStoreMatch.AssistantId, string.Join("/", existingIds), string.Join("/", desiredIds), string.Join("/", removedCustomerIds));
                
                await CopySplitCustomersBeforeShrinkAsync(
                    serviceProviderId, initiatedByUserId, removedCustomerIds,
                    mergedGroup.SalesKey, customerIdLookup, storeMap, salesAgentCache, customerKnowledgeAssistantCache,
                    sourceSceneLookup,
                    existingCrmAssistants, claimedAssistantIds, stats, cancellationToken).ConfigureAwait(false);
            }

            claimedAssistantIds.Add(sameStoreMatch.AssistantId);
            if (!string.Equals(sameStoreMatch.Name, assistantName, StringComparison.OrdinalIgnoreCase))
            {
                await RenameCustomerAssistantAsync(sameStoreMatch, assistantName, stats, cancellationToken).ConfigureAwait(false);
                sameStoreMatch.Name = assistantName;
            }

            return;
        }

        var crossStoreMatch = FindBestMatchingAssistant(existingCrmAssistantsById, assistantCustomerIdsByAssistantId, assistantIdsByCustomerId, desiredIds, null, claimedAssistantIds);
        if (crossStoreMatch != null)
        {
            if (assistantCustomerIdsByAssistantId.TryGetValue(crossStoreMatch.AssistantId, out var existingIds) && existingIds.SetEquals(desiredIds))
            {
                Log.Information(
                    "Assistant match: cross-store transfer. AssistantId={AssistantId}, StoreId={CurrentStoreId}, TargetStoreId={TargetStoreId}",
                    crossStoreMatch.AssistantId, crossStoreMatch.StoreId, targetStoreId);
                claimedAssistantIds.Add(crossStoreMatch.AssistantId);
                await TransferCustomerAssistantToStoreAsync(crossStoreMatch, targetStoreId, stats, cancellationToken).ConfigureAwait(false);
                crossStoreMatch.StoreId = targetStoreId;
                stats.TransferredAssistantCount++;
                
                return;
            }

            if (assistantCustomerIdsByAssistantId.TryGetValue(crossStoreMatch.AssistantId, out existingIds) && existingIds.IsSupersetOf(desiredIds) && !existingIds.SetEquals(desiredIds))
            {
                Log.Information(
                    "Assistant match: cross-store superset. SourceAssistantId={AssistantId}, Source={MatchedAssistantName}, Target={TargetAssistantName}",
                    crossStoreMatch.AssistantId, crossStoreMatch.Name, assistantName);
               
                var copiedAssistant = await EnsureCustomerKnowledgeAssistantAsync(
                    serviceProviderId, initiatedByUserId, salesAgentId, targetStoreId, assistantName, mergedGroup.Language,
                    mergedGroup.CustomerIds, sourceSceneLookup,
                    customerKnowledgeAssistantCache, stats, cancellationToken).ConfigureAwait(false);
               
                claimedAssistantIds.Add(copiedAssistant.Id);
                existingCrmAssistants.Add(new CrmAutoSyncAssistantLocationDto
                {
                    AssistantId = copiedAssistant.Id,
                    StoreId = targetStoreId,
                    Name = assistantName
                });
                existingCrmAssistantsByName[assistantName] = existingCrmAssistants[^1];
                assistantCustomerIdsByAssistantId[copiedAssistant.Id] = desiredIds;
                AddAssistantIdsByCustomerId(assistantIdsByCustomerId, desiredIds, copiedAssistant.Id);
                Log.Information("Assistant split created. AssistantId={AssistantId}, Name={AssistantName}, StoreId={StoreId}",
                    copiedAssistant.Id, assistantName, targetStoreId);
                return;
            }
        }

        Log.Information(
            "Assistant match: none. Name={AssistantName}, StoreId={TargetStoreId}",
            assistantName, targetStoreId);
        var createdAssistant = await EnsureCustomerKnowledgeAssistantAsync(
            serviceProviderId, initiatedByUserId, salesAgentId, targetStoreId, assistantName, mergedGroup.Language,
            mergedGroup.CustomerIds, sourceSceneLookup,
            customerKnowledgeAssistantCache, stats, cancellationToken).ConfigureAwait(false);
        
        claimedAssistantIds.Add(createdAssistant.Id);
        existingCrmAssistants.Add(new CrmAutoSyncAssistantLocationDto
        {
            AssistantId = createdAssistant.Id,
            StoreId = targetStoreId,
            Name = assistantName
        });
        existingCrmAssistantsByName[assistantName] = existingCrmAssistants[^1];
        assistantCustomerIdsByAssistantId[createdAssistant.Id] = desiredIds;
        AddAssistantIdsByCustomerId(assistantIdsByCustomerId, desiredIds, createdAssistant.Id);
    }

    private static CrmAutoSyncAssistantLocationDto FindBestMatchingAssistant(
        IReadOnlyDictionary<int, CrmAutoSyncAssistantLocationDto> assistantsById,
        IReadOnlyDictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId,
        IReadOnlyDictionary<string, HashSet<int>> assistantIdsByCustomerId,
        HashSet<string> desiredIds,
        int? storeId,
        HashSet<int> claimedAssistantIds)
    {
        HashSet<int>? candidateAssistantIds = null;
        foreach (var customerId in desiredIds)
        {
            if (!assistantIdsByCustomerId.TryGetValue(customerId, out var ids) || ids.Count == 0)
                continue;

            candidateAssistantIds ??= new HashSet<int>();
            candidateAssistantIds.UnionWith(ids);
        }

        if (candidateAssistantIds == null || candidateAssistantIds.Count == 0)
            return null;

        CrmAutoSyncAssistantLocationDto? bestAssistant = null;
        var bestOverlapCount = -1;
        var bestIsExact = false;

        foreach (var assistantId in candidateAssistantIds)
        {
            if (claimedAssistantIds.Contains(assistantId))
                continue;

            if (!assistantsById.TryGetValue(assistantId, out var assistant))
                continue;

            if (storeId != null && assistant.StoreId != storeId)
                continue;

            if (!assistantCustomerIdsByAssistantId.TryGetValue(assistantId, out var existingIds) || existingIds.Count == 0)
                continue;

            var overlapCount = CountOverlaps(existingIds, desiredIds);
            if (overlapCount <= 0)
                continue;

            var isExact = existingIds.SetEquals(desiredIds);
            if (overlapCount > bestOverlapCount || (overlapCount == bestOverlapCount && isExact && !bestIsExact))
            {
                bestAssistant = assistant;
                bestOverlapCount = overlapCount;
                bestIsExact = isExact;
            }
        }

        return bestAssistant;
    }

    private static bool TryParseAssistantIds(string assistantName, out HashSet<string> customerIds)
    {
        customerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!CrmSalesAutoSyncGrouping.TryParseAssistantName(assistantName, out var ids, out _))
            return false;

        customerIds = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return customerIds.Count > 0;
    }

    private static Dictionary<string, HashSet<int>> BuildAssistantIdsByCustomerId(Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId)
    {
        var lookup = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (assistantId, customerIds) in assistantCustomerIdsByAssistantId)
        {
            foreach (var customerId in customerIds)
            {
                if (!lookup.TryGetValue(customerId, out var assistantIds))
                {
                    assistantIds = new HashSet<int>();
                    lookup[customerId] = assistantIds;
                }

                assistantIds.Add(assistantId);
            }
        }

        return lookup;
    }

    private static void AddAssistantIdsByCustomerId(Dictionary<string, HashSet<int>> lookup, IEnumerable<string> customerIds, int assistantId)
    {
        foreach (var customerId in customerIds)
        {
            if (!lookup.TryGetValue(customerId, out var assistantIds))
            {
                assistantIds = new HashSet<int>();
                lookup[customerId] = assistantIds;
            }

            assistantIds.Add(assistantId);
        }
    }

    private static int CountOverlaps(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count > right.Count)
            (left, right) = (right, left);

        var overlap = 0;
        foreach (var item in left)
        {
            if (right.Contains(item))
                overlap++;
        }

        return overlap;
    }

    private async Task RenameCustomerAssistantAsync(
        CrmAutoSyncAssistantLocationDto assistantLocation, string assistantName, AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(assistantLocation.AssistantId, cancellationToken).ConfigureAwait(false);
        if (assistant == null || string.Equals(assistant.Name, assistantName, StringComparison.OrdinalIgnoreCase))
            return;

        var previousAssistantName = assistant.Name;
        assistant.Name = assistantName;
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        RecordRenamedAssistant(stats, assistant.Id, assistantLocation.StoreId, assistantLocation.AgentId, assistantName, previousAssistantName);
        Log.Information("Assistant renamed. AssistantId={AssistantId}, From={FromName}, To={ToName}",
            assistant.Id, previousAssistantName, assistantName);
    }

    private async Task CopySplitCustomersBeforeShrinkAsync(
        int serviceProviderId,
        int? initiatedByUserId,
        IEnumerable<string> removedCustomerIds,
        string currentSalesKey, IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup,
        Dictionary<string, CompanyStore> storeMap,
        Dictionary<string, Agent> salesAgentCache,
        Dictionary<string, Core.Domain.AISpeechAssistant.AiSpeechAssistant> customerKnowledgeAssistantCache,
        SourceSceneLookup sourceSceneLookup,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        HashSet<int> claimedAssistantIds,
        AiResourceSyncExecutionStatsDto stats,
        CancellationToken cancellationToken)
    {
        var processedTargetGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removedCustomerIdList = removedCustomerIds.ToList();

        Log.Information(
            "Split start. SalesKey={CurrentSalesKey}, Removed={RemovedCustomerIds}",
            currentSalesKey, string.Join("/", removedCustomerIdList));

        foreach (var removedCustomerId in removedCustomerIdList)
        {
            if (!customerIdLookup.TryGetValue(removedCustomerId, out var targetGroup))
            {
                Log.Warning(
                    "Split skip: group missing. CustomerId={CustomerId}, SalesKey={CurrentSalesKey}",
                    removedCustomerId, currentSalesKey);
                continue;
            }

            if (string.Equals(targetGroup.SalesKey, currentSalesKey, StringComparison.OrdinalIgnoreCase))
            {
                Log.Information(
                    "Split skip: same sales. CustomerId={CustomerId}, SalesKey={SalesKey}",
                    removedCustomerId, currentSalesKey);
                continue;
            }

            var targetGroupKey = $"{targetGroup.SalesKey}|{CrmSalesAutoSyncGrouping.BuildAssistantName(targetGroup.CustomerIds, targetGroup.Language)}";
            Log.Information("Split target. CustomerId={CustomerId}, TargetKey={TargetGroupKey}", removedCustomerId, targetGroupKey);
            if (!processedTargetGroups.Add(targetGroupKey))
            {
                Log.Information(
                    "Split skip: duplicate target. CustomerId={CustomerId}, TargetKey={TargetGroupKey}",
                    removedCustomerId, targetGroupKey);
                continue;
            }

            if (!storeMap.TryGetValue(targetGroup.SalesKey, out var targetStore))
            {
                Log.Warning(
                    "Split skip: store missing. CustomerId={CustomerId}, TargetSalesKey={TargetSalesKey}",
                    removedCustomerId, targetGroup.SalesKey);
                continue;
            }

            var targetSalesAgent = await EnsureSalesAgentAsync(
                serviceProviderId, targetStore.Id, targetGroup.SalesKey, salesAgentCache, stats, cancellationToken).ConfigureAwait(false);
            
            var targetAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(targetGroup.CustomerIds, targetGroup.Language);
            var copiedAssistant = await EnsureCustomerKnowledgeAssistantAsync(
                serviceProviderId, initiatedByUserId, targetSalesAgent.Id, targetStore.Id, targetAssistantName, 
                targetGroup.Language, targetGroup.CustomerIds, sourceSceneLookup,
                customerKnowledgeAssistantCache, stats, cancellationToken).ConfigureAwait(false);

            claimedAssistantIds.Add(copiedAssistant.Id);
            existingCrmAssistants.Add(new CrmAutoSyncAssistantLocationDto
            {
                AssistantId = copiedAssistant.Id,
                StoreId = targetStore.Id,
                Name = targetAssistantName
            });

            Log.Information(
                "Split created. AssistantId={AssistantId}, Name={AssistantName}, StoreId={TargetStoreId}, SalesKey={TargetSalesKey}, Customers={CustomerIds}, Lang={Language}",
                copiedAssistant.Id, targetAssistantName, targetStore.Id, targetGroup.SalesKey, string.Join("/", targetGroup.CustomerIds), targetGroup.Language ?? "英文");
        }
    }

    private async Task ReconcileInactiveCustomerAssistantsAsync(
        HashSet<string> activeCustomerIds, List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants, Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId, HashSet<int> claimedAssistantIds,
        AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        foreach (var assistant in existingCrmAssistants.Where(x => !claimedAssistantIds.Contains(x.AssistantId)))
        {
            if (!assistantCustomerIdsByAssistantId.TryGetValue(assistant.AssistantId, out var existingIds) || existingIds.Count == 0)
                continue;

            _ = CrmSalesAutoSyncGrouping.TryParseAssistantName(assistant.Name, out _, out var language);

            var remainingIds = existingIds
                .Where(activeCustomerIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (remainingIds.Count == 0)
            {
                Log.Information("Assistant deactivate. AssistantId={AssistantId}, Name={AssistantName}", assistant.AssistantId, assistant.Name);
                await DeactivateCustomerAssistantKnowledgeAsync(assistant.AssistantId, cancellationToken).ConfigureAwait(false);
                stats.DeactivatedAssistantCount++;
                RecordDeactivatedAssistant(stats, assistant.AssistantId, assistant.StoreId, assistant.AgentId, assistant.Name);
                stats.Warnings.Add($"Assistant [{assistant.Name}] has no active CRM customers remaining; knowledge deactivated.");
                continue;
            }

            if (remainingIds.Count == existingIds.Count)
                continue;

            var renamedAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(remainingIds, language);
            Log.Information("Assistant shrink. AssistantId={AssistantId}, From={FromName}, To={ToName}",
                assistant.AssistantId, assistant.Name, renamedAssistantName);
            await RenameCustomerAssistantAsync(assistant, renamedAssistantName, stats, cancellationToken).ConfigureAwait(false);
            assistant.Name = renamedAssistantName;
            assistantCustomerIdsByAssistantId[assistant.AssistantId] = remainingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task DeactivateCustomerAssistantKnowledgeAsync(int assistantId, CancellationToken cancellationToken)
    {
        var activeKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            assistantId: assistantId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (activeKnowledge == null)
        {
            Log.Information("Knowledge deactivate skip. AssistantId={AssistantId}", assistantId);
            return;
        }

        activeKnowledge.IsActive = false;
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync([activeKnowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("Knowledge deactivated. AssistantId={AssistantId}, KnowledgeId={KnowledgeId}", assistantId, activeKnowledge.Id);
    }

    private async Task TransferCustomerAssistantToStoreAsync(
        CrmAutoSyncAssistantLocationDto assistantLocation, int targetStoreId, AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        var posAgent = await _posDataProvider.GetPosAgentByAgentIdAsync(assistantLocation.AgentId, cancellationToken).ConfigureAwait(false);

        if (posAgent == null || posAgent.StoreId == targetStoreId)
        {
            Log.Information("Transfer skip. AssistantId={AssistantId}, TargetStoreId={TargetStoreId}", assistantLocation.AssistantId, targetStoreId);
            return;
        }

        var previousStoreId = posAgent.StoreId;
        posAgent.StoreId = targetStoreId;
        await _posDataProvider.UpdatePosAgentsAsync([posAgent], cancellationToken: cancellationToken).ConfigureAwait(false);
        RecordTransferredAssistant(stats, assistantLocation.AssistantId, targetStoreId, assistantLocation.AgentId, assistantLocation.Name, previousStoreId);
        Log.Information("Assistant transferred. AssistantId={AssistantId}, FromStoreId={FromStoreId}, ToStoreId={ToStoreId}",
            assistantLocation.AssistantId, previousStoreId, targetStoreId);
    }

    private async Task<SourceSceneLookup> BuildSourceSceneLookupAsync(int companyId, CancellationToken cancellationToken)
    {
        var mappings = await _knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(
            companyId: companyId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var mappingSceneIds = mappings
            .GroupBy(x => x.Language)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.CreatedAt).First().SceneId);

        if (mappingSceneIds.Count == 0)
            return new SourceSceneLookup(new Dictionary<AutoAddLanguage, KnowledgeScene>(), new Dictionary<int, List<KnowledgeSceneItem>>());

        var scenes = await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(
            mappingSceneIds.Values.Distinct().ToList(), cancellationToken).ConfigureAwait(false);
        var sceneMap = scenes.ToDictionary(x => x.Id);
        var mappingScenes = mappingSceneIds
            .Where(x => sceneMap.ContainsKey(x.Value))
            .ToDictionary(x => x.Key, x => sceneMap[x.Value]);
        var sceneItemsLookup = new Dictionary<int, List<KnowledgeSceneItem>>();

        foreach (var sceneId in mappingScenes.Values.Select(x => x.Id).Distinct())
        {
            var sceneItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdAsync(sceneId, cancellationToken).ConfigureAwait(false);
            sceneItemsLookup[sceneId] = sceneItems;
        }

        return new SourceSceneLookup(mappingScenes, sceneItemsLookup);
    }

    private KnowledgeScene ResolveSourceScene(SourceSceneLookup sourceSceneLookup, string language)
    {
        var rawLanguage = string.IsNullOrWhiteSpace(language) ? "英文" : language;
        var normalizedLanguage = CrmToAutoAddLanguageConverter.TryResolve(rawLanguage, out var resolvedLanguage)
            ? resolvedLanguage
            : AutoAddLanguage.English;

        return sourceSceneLookup.MappingScenes.TryGetValue(normalizedLanguage, out var scene)
            ? scene
            : null;
    }

    private static void RecordCreatedStore(AiResourceSyncExecutionStatsDto stats, int storeId, string storeName)
    {
        stats.CreatedStores.Add(new AiResourceSyncCreateStoreChangeDto
        {
            StoreId = storeId,
            StoreName = storeName
        });
    }

    private static void RecordCreatedAgent(AiResourceSyncExecutionStatsDto stats, int agentId, int storeId, string agentName)
    {
        stats.CreatedAgents.Add(new AiResourceSyncCreateAgentChangeDto
        {
            AgentId = agentId,
            StoreId = storeId,
            AgentName = agentName
        });
    }

    private static void RecordCreatedAssistant(AiResourceSyncExecutionStatsDto stats, int assistantId, int? storeId, int? agentId, string assistantName)
    {
        stats.CreatedAssistants.Add(new AiResourceSyncCreateAssistantChangeDto
        {
            AssistantId = assistantId,
            StoreId = storeId,
            AgentId = agentId,
            AssistantName = assistantName
        });
    }

    private static void RecordTransferredAssistant(AiResourceSyncExecutionStatsDto stats, int assistantId, int? storeId, int? agentId, string assistantName, int? previousStoreId)
    {
        stats.TransferredAssistants.Add(new AiResourceSyncCreateAssistantChangeDto
        {
            AssistantId = assistantId,
            StoreId = storeId,
            AgentId = agentId,
            AssistantName = assistantName,
            PreviousStoreId = previousStoreId
        });
    }

    private static void RecordRenamedAssistant(AiResourceSyncExecutionStatsDto stats, int assistantId, int? storeId, int? agentId, string assistantName, string previousAssistantName)
    {
        stats.RenamedAssistants.Add(new AiResourceSyncCreateAssistantChangeDto
        {
            AssistantId = assistantId,
            StoreId = storeId,
            AgentId = agentId,
            AssistantName = assistantName,
            PreviousAssistantName = previousAssistantName
        });
    }

    private static void RecordDeactivatedAssistant(AiResourceSyncExecutionStatsDto stats, int assistantId, int? storeId, int? agentId, string assistantName)
    {
        stats.DeactivatedAssistants.Add(new AiResourceSyncCreateAssistantChangeDto
        {
            AssistantId = assistantId,
            StoreId = storeId,
            AgentId = agentId,
            AssistantName = assistantName
        });
    }

    private static void MergeStats(AiResourceSyncExecutionStatsDto target, AiResourceSyncExecutionStatsDto source)
    {
        target.CreatedStoreCount += source.CreatedStoreCount;
        target.CreatedAgentCount += source.CreatedAgentCount;
        target.CreatedAssistantCount += source.CreatedAssistantCount;
        target.CreatedKnowledgeCount += source.CreatedKnowledgeCount;
        target.AppliedSceneCount += source.AppliedSceneCount;
        target.TransferredAssistantCount += source.TransferredAssistantCount;
        target.DeactivatedAssistantCount += source.DeactivatedAssistantCount;

        target.CreatedStores.AddRange(source.CreatedStores);
        target.CreatedAgents.AddRange(source.CreatedAgents);
        target.CreatedAssistants.AddRange(source.CreatedAssistants);
        target.TransferredAssistants.AddRange(source.TransferredAssistants);
        target.RenamedAssistants.AddRange(source.RenamedAssistants);
        target.DeactivatedAssistants.AddRange(source.DeactivatedAssistants);
        target.Warnings.AddRange(source.Warnings);
    }

    public async Task RecordSyncRunAsync(AiResourceSyncCommand command, AiResourceSyncExecutionStatsDto stats, bool isInitialRelease, bool isSuccess, string errorMessage, CancellationToken cancellationToken)
    {
        var createdStores = BuildPersistedDetailSection(stats?.CreatedStores);
        var createdAgents = BuildPersistedDetailSection(stats?.CreatedAgents);
        var createdAssistants = BuildPersistedDetailSection(stats?.CreatedAssistants);
        var transferredAssistants = BuildPersistedDetailSection(stats?.TransferredAssistants);
        var renamedAssistants = BuildPersistedDetailSection(stats?.RenamedAssistants);
        var deactivatedAssistants = BuildPersistedDetailSection(stats?.DeactivatedAssistants);

        var run = new CrmSalesAutoSyncRun
        {
            Mode = isInitialRelease ? "initial" : command.IsManual ? "manual" : "automatic",
            IsSuccess = isSuccess,
            TotalCount = stats?.TotalCount ?? 0,
            CreatedStoreCount = stats?.CreatedStoreCount ?? 0,
            CreatedAgentCount = stats?.CreatedAgentCount ?? 0,
            CreatedAssistantCount = stats?.CreatedAssistantCount ?? 0,
            CreatedKnowledgeCount = stats?.CreatedKnowledgeCount ?? 0,
            AppliedSceneCount = stats?.AppliedSceneCount ?? 0,
            TransferredAssistantCount = stats?.TransferredAssistantCount ?? 0,
            DeactivatedAssistantCount = stats?.DeactivatedAssistantCount ?? 0,
            WarningsJson = BuildPersistedWarningsJson(stats),
            CreatedStoresJson = createdStores == null ? null : JsonConvert.SerializeObject(createdStores),
            CreatedAgentsJson = createdAgents == null ? null : JsonConvert.SerializeObject(createdAgents),
            CreatedAssistantsJson = createdAssistants == null ? null : JsonConvert.SerializeObject(createdAssistants),
            TransferredAssistantsJson = transferredAssistants == null ? null : JsonConvert.SerializeObject(transferredAssistants),
            RenamedAssistantsJson = renamedAssistants == null ? null : JsonConvert.SerializeObject(renamedAssistants),
            DeactivatedAssistantsJson = deactivatedAssistants == null ? null : JsonConvert.SerializeObject(deactivatedAssistants),
            ErrorMessage = errorMessage
        };

        await _salesDataProvider.AddCrmSalesAutoSyncRunAsync(run, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task SendNotifyAsync(bool isSuccess, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_aiResourceSyncSetting.NotifyRobotUrl))
            return;

        var currentTime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var content = isSuccess
            ? $"✅SMT Sales Auto Create: Success\nTime: {currentTime}"
            : $"❌SMT Sales Auto Create: Failed\nTime: {currentTime}";
        var text = new SendWorkWechatGroupRobotTextDto
        {
            Content = content,
            MentionedMobileList = "@all"
        };

        await _weChatClient.SendWorkWechatRobotMessagesAsync(_aiResourceSyncSetting.NotifyRobotUrl,
            new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = text
            }, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildStoreScopedCacheKey(int storeId, string name)
    {
        return $"{storeId}:{name}";
    }

    private static string BuildPersistedWarningsJson(AiResourceSyncExecutionStatsDto stats)
    {
        if (stats == null)
            return null;

        var warnings = stats.Warnings.Take(MaxWarningEntriesToPersist).ToList();
        if (stats.Warnings.Count > MaxWarningEntriesToPersist)
        {
            warnings.Add($"... truncated {stats.Warnings.Count - MaxWarningEntriesToPersist} warning entries");
        }

        return JsonConvert.SerializeObject(warnings);
    }

    private static object BuildPersistedDetailSection<T>(List<T> items)
    {
        items ??= [];
        var persistedItems = items.Take(MaxDetailEntriesPerCategoryToPersist).ToList();

        return new
        {
            TotalCount = items.Count,
            TruncatedCount = Math.Max(0, items.Count - persistedItems.Count),
            Items = persistedItems
        };
    }

    private static string BuildStoreNamesJson(string storeName)
    {
        return JsonConvert.SerializeObject(new PosNamesLocalization
        {
            En = new PosNamesDetail { Name = storeName },
            Cn = new PosNamesDetail { Name = storeName }
        }, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    private static string GetStoreName(string names)
    {
        try
        {
            return JsonConvert.DeserializeObject<PosNamesLocalization>(names)?.En?.Name ?? names;
        }
        catch
        {
            return names;
        }
    }
}
