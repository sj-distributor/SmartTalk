using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
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
using SmartTalk.Core.Utils;
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

namespace SmartTalk.Core.Services.AiResourceSync;

public partial interface IAiResourceSyncService : IScopedDependency
{
    Task<AiResourceSyncExecutionResult> SyncInternalAsync(AiResourceSyncCommand command, CancellationToken cancellationToken);
    
    Task RecordSyncRunAsync(AiResourceSyncCommand command, AiResourceSyncExecutionStatsDto stats, bool isInitialRelease, bool isSuccess, string errorMessage, CancellationToken cancellationToken);

    Task SendNotifyAsync(bool isSuccess, bool isManual, CancellationToken cancellationToken);
}

public partial class AiResourceSyncService : IAiResourceSyncService
{
    private const int MaxWarningEntriesToPersist = 100;
    private const int MaxDetailEntriesPerCategoryToPersist = 50;
    private const int MaxConcurrentCrmCustomerLookups = 8;

    public sealed record StoreLockResult(CompanyStore Store, bool IsCreated);
 
    private readonly IMediator _mediator;
    private readonly ICrmClient _crmClient;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IAiSpeechAssistantKnowledgePromptService _aiSpeechAssistantKnowledgePromptService;
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
        IAiSpeechAssistantKnowledgePromptService aiSpeechAssistantKnowledgePromptService,
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
        _aiSpeechAssistantKnowledgePromptService = aiSpeechAssistantKnowledgePromptService;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
        _salesDataProvider = salesDataProvider;
        _weChatClient = weChatClient;
        _redisSafeRunner = redisSafeRunner;
        _salesSetting = salesSetting;
        _aiResourceSyncSetting = aiResourceSyncSetting;
    }

    public async Task<AiResourceSyncExecutionResult> SyncInternalAsync(AiResourceSyncCommand command, CancellationToken cancellationToken)
    {
        var inputContext = await LoadSyncInputContextAsync(command, cancellationToken).ConfigureAwait(false);
        Log.Information("CRM sync start. Customers={CustomerCount}, IsFullSync={IsFullSync}, IsInitialRelease={IsInitialRelease}",
            inputContext.Customers.Count, inputContext.IsFullSync, inputContext.IsInitialRelease);

        if (inputContext.Customers.Count == 0)
        {
            return new AiResourceSyncExecutionResult
            {
                TotalCount = 0,
                ShardCount = 0,
                Shards = [],
                Stats = new AiResourceSyncExecutionStatsDto { TotalCount = 0 },
                IsInitialRelease = inputContext.IsInitialRelease
            };
        }

        var executionContext = await BuildSyncExecutionContextAsync(inputContext.Company, inputContext.CustomerGroups, cancellationToken).ConfigureAwait(false);
        var shardResults = await ExecuteShardSyncAsync(command, inputContext, executionContext, cancellationToken).ConfigureAwait(false);
        await FinalizeSyncAsync(inputContext, executionContext.AssistantContext, cancellationToken).ConfigureAwait(false);

        return new AiResourceSyncExecutionResult
        {
            TotalCount = inputContext.Customers.Count,
            ShardCount = shardResults.Count,
            Shards = shardResults,
            Stats = inputContext.Stats,
            IsInitialRelease = inputContext.IsInitialRelease
        };
    }
    
    private async Task<AiResourceSyncInputContext> LoadSyncInputContextAsync(AiResourceSyncCommand command, CancellationToken cancellationToken)
    {
        var customerLoadResult = await LoadSyncCustomersAsync(command, cancellationToken).ConfigureAwait(false);
        var customers = customerLoadResult.Customers;
        var customerGroups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);

        return new AiResourceSyncInputContext
        {
            Company = customerLoadResult.Company,
            Customers = customers,
            CustomerGroups = customerGroups,
            CustomerIdLookup = CrmSalesAutoSyncGrouping.BuildCustomerIdLookup(customerGroups),
            ActiveCustomerIds = CrmSalesAutoSyncGrouping.BuildActiveCustomerIds(customers),
            IsFullSync = customerLoadResult.IsFullSync,
            IsInitialRelease = customerLoadResult.IsInitialRelease,
            Stats = new AiResourceSyncExecutionStatsDto { TotalCount = customers.Count }
        };
    }

    private async Task<AiResourceSyncExecutionContext> BuildSyncExecutionContextAsync(
        Company company, List<CrmSalesAutoSyncCustomerGroup> customerGroups, CancellationToken cancellationToken)
    {
        var existingCrmAssistants = await LoadExistingCrmAssistantsAsync(company.Id, cancellationToken).ConfigureAwait(false);
        var existingStores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("Stores loaded. Count={StoreCount}", existingStores.Count);

        var sourceSceneLookup = await BuildSourceSceneLookupAsync(company.Id, cancellationToken).ConfigureAwait(false);
        Log.Information("Scenes loaded. Count={SceneCount}", sourceSceneLookup.MappingScenes.Count);

        if (company.Name.Equals(_salesSetting.CompanyName, StringComparison.OrdinalIgnoreCase) && sourceSceneLookup.MappingScenes.Count == 0)
            throw new Exception($"Sales company [{_salesSetting.CompanyName}] has no active knowledge scene mapping.");

        return new AiResourceSyncExecutionContext
        {
            StoreContext = BuildStoreContext(existingStores, customerGroups),
            AssistantContext = BuildAssistantContext(existingCrmAssistants),
            SourceSceneLookup = sourceSceneLookup,
            SyncTasks = BuildSyncTasks(customerGroups)
        };
    }

    private async Task<List<AiResourceSyncShardExecutionResult>> ExecuteShardSyncAsync(
        AiResourceSyncCommand command, AiResourceSyncInputContext inputContext, AiResourceSyncExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var shardResults = new List<AiResourceSyncShardExecutionResult>();
        Log.Information(
            "Sync shards start. ShardCount={ShardCount}",
            executionContext.SyncTasks.Select(x => x.StoreName).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var salesShard in executionContext.SyncTasks
                     .OrderBy(x => x.StoreName, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(x => x.StoreName, StringComparer.OrdinalIgnoreCase))
        {
            Log.Information("Sync shard start. StoreName={StoreName}, TaskCount={TaskCount}", salesShard.Key, salesShard.Count());

            var shardResult = await ExecuteSalesShardAsync(
                command, inputContext.Company.Id, salesShard.Key, salesShard.ToList(), inputContext.CustomerIdLookup, 
                executionContext.StoreContext, executionContext.AssistantContext, executionContext.SourceSceneLookup, cancellationToken).ConfigureAwait(false);

            shardResults.Add(shardResult);
            MergeStats(inputContext.Stats, shardResult.Stats);

            Log.Information(
                "Sync shard done. StoreName={StoreName}, CreatedAssistantCount={CreatedAssistantCount}, TransferredAssistantCount={TransferredAssistantCount}, DeactivatedAssistantCount={DeactivatedAssistantCount}",
                salesShard.Key,
                shardResult.Stats.CreatedAssistantCount,
                shardResult.Stats.TransferredAssistantCount,
                shardResult.Stats.DeactivatedAssistantCount);
        }

        return shardResults;
    }

    private async Task FinalizeSyncAsync(AiResourceSyncInputContext inputContext, AiResourceSyncAssistantContext assistantContext, CancellationToken cancellationToken)
    {
        if (!inputContext.IsFullSync)
            return;

        Log.Information(
            "Reconcile inactive assistants start. AssistantCount={AssistantCount}, ClaimedCount={ClaimedCount}",
            assistantContext.ExistingCrmAssistants.Count,
            assistantContext.ClaimedAssistantIds.Count);

        await ReconcileInactiveCustomerAssistantsAsync(
            inputContext.ActiveCustomerIds,
            assistantContext.ExistingCrmAssistants,
            assistantContext.AssistantCustomerIdsByAssistantId,
            assistantContext.ClaimedAssistantIds,
            inputContext.Stats,
            cancellationToken).ConfigureAwait(false);

        Log.Information(
            "Reconcile inactive assistants done. DeactivatedAssistantCount={DeactivatedAssistantCount}",
            inputContext.Stats.DeactivatedAssistantCount);
    }

    private async Task<List<CrmAutoSyncAssistantLocationDto>> LoadExistingCrmAssistantsAsync(int companyId, CancellationToken cancellationToken)
    {
        var loadedCrmAssistants = await _aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(companyId, cancellationToken).ConfigureAwait(false);
        var duplicateAssistantIds = loadedCrmAssistants
            .GroupBy(x => x.AssistantId)
            .Where(x => x.Key > 0 && x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateAssistantIds.Count > 0)
        {
            Log.Warning("Duplicate CRM assistant locations found. AssistantIds={AssistantIds}", string.Join(",", duplicateAssistantIds));
        }

        var existingCrmAssistants = loadedCrmAssistants
            .Where(x => x.AssistantId > 0)
            .GroupBy(x => x.AssistantId)
            .Select(x => x.First())
            .ToList();
        Log.Information("Assistants loaded. Count={AssistantCount}", existingCrmAssistants.Count);

        return existingCrmAssistants;
    }

    private static AiResourceSyncStoreContext BuildStoreContext(
        List<CompanyStore> existingStores, List<CrmSalesAutoSyncCustomerGroup> customerGroups)
    {
        var existingStoreNamesById = existingStores
            .Select(x => new { x.Id, StoreName = GetStoreName(x.Names) })
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().StoreName);
      
        var storeNames = customerGroups
            .Select(x => x.SalesKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var storeMap = existingStores
            .Select(x => new { Store = x, StoreName = GetStoreName(x.Names) })
            .Where(x => !string.IsNullOrWhiteSpace(x.StoreName) && storeNames.Contains(x.StoreName))
            .GroupBy(x => x.StoreName)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Store.CreatedDate).First().Store, StringComparer.OrdinalIgnoreCase);

        return new AiResourceSyncStoreContext
        {
            StoreMap = storeMap,
            ExistingStoreNamesById = existingStoreNamesById
        };
    }

    private static AiResourceSyncAssistantContext BuildAssistantContext(List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants)
    {
        var claimedAssistantIds = new HashSet<int>();
        var existingCrmAssistantsByName = existingCrmAssistants
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name)
            .ToDictionary(x => x.Key, x => x.First());
       
        var existingCrmAssistantsById = existingCrmAssistants
            .GroupBy(x => x.AssistantId)
            .ToDictionary(x => x.Key, x => x.First());
       
        var assistantCustomerIdsByAssistantId = existingCrmAssistants
            .ToDictionary(
                x => x.AssistantId,
                x => TryParseAssistantIds(x.Name, out var ids)
                    ? ids
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        return new AiResourceSyncAssistantContext
        {
            ExistingCrmAssistants = existingCrmAssistants,
            ExistingCrmAssistantsById = existingCrmAssistantsById,
            ExistingCrmAssistantsByName = existingCrmAssistantsByName,
            AssistantCustomerIdsByAssistantId = assistantCustomerIdsByAssistantId,
            AssistantIdsByCustomerId = BuildAssistantIdsByCustomerId(assistantCustomerIdsByAssistantId),
            ClaimedAssistantIds = claimedAssistantIds,
            SalesAgentCache = new Dictionary<string, Agent>(StringComparer.OrdinalIgnoreCase),
            CustomerKnowledgeAssistantCache = new Dictionary<string, Core.Domain.AISpeechAssistant.AiSpeechAssistant>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static List<SalesKnowledgeSyncTask> BuildSyncTasks(List<CrmSalesAutoSyncCustomerGroup> customerGroups)
    {
        return customerGroups
            .GroupBy(x => x.SalesKey)
            .SelectMany(salesBucket =>
            {
                var seedCustomer = salesBucket.First().Customers.First();
                return salesBucket.Select(mergedGroup => new SalesKnowledgeSyncTask
                {
                    StoreName = salesBucket.Key,
                    SeedCustomer = seedCustomer,
                    MergedGroup = mergedGroup
                });
            })
            .ToList();
    }

    private async Task<AiResourceSyncCustomerLoadResult> LoadSyncCustomersAsync(AiResourceSyncCommand command, CancellationToken cancellationToken)
    {
        var company = await _posDataProvider.GetPosCompanyByNameAsync(_salesSetting.CompanyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
            throw new Exception($"Sales company [{_salesSetting.CompanyName}] not found.");

        var isInitialRelease = !command.IsManual
            && !await _aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(company.Id, cancellationToken).ConfigureAwait(false);

        if (isInitialRelease || command.IsFullSync)
        {
            var (customers, totalCount) = await _crmClient.GetSalesAutoSyncCustomersAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return new AiResourceSyncCustomerLoadResult
            {
                Company = company,
                Customers = customers,
                TotalCount = totalCount ?? customers.Count,
                IsFullSync = true,
                IsInitialRelease = isInitialRelease
            };
        }

        var customersChanged = await _crmClient.GetChangedSalesAutoSyncCustomersAsync(cancellationToken).ConfigureAwait(false);
        return new AiResourceSyncCustomerLoadResult
        {
            Company = company,
            Customers = customersChanged,
            TotalCount = customersChanged.Count,
            IsFullSync = false,
            IsInitialRelease = false
        };
    }

    private async Task<AiResourceSyncShardExecutionResult> ExecuteSalesShardAsync(
        AiResourceSyncCommand command, int companyId, string salesKey,
        IReadOnlyList<SalesKnowledgeSyncTask> syncTasks, IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup, AiResourceSyncStoreContext storeContext,
        AiResourceSyncAssistantContext assistantContext, SourceSceneLookup sourceSceneLookup, CancellationToken cancellationToken)
    {
        var stats = new AiResourceSyncExecutionStatsDto();
        var seedCustomer = syncTasks.First().SeedCustomer;

        var store = await EnsureSalesStoreAsync(
            seedCustomer, companyId, storeContext.StoreMap, command.InitiatedByUserId, stats, cancellationToken).ConfigureAwait(false);

        var salesAgent = await EnsureSalesAgentAsync(
            command.ServiceProviderId.Value, store.Id, salesKey, assistantContext.SalesAgentCache, stats, cancellationToken).ConfigureAwait(false);

        foreach (var syncTask in syncTasks)
        {
            await EnsureMergedCustomerKnowledgeAsync(
                command.ServiceProviderId.Value, command.InitiatedByUserId, companyId, store.Id, salesAgent.Id, syncTask.MergedGroup, customerIdLookup,
                storeContext, assistantContext, sourceSceneLookup, stats, cancellationToken).ConfigureAwait(false);
        }

        return new AiResourceSyncShardExecutionResult
        {
            SalesKey = salesKey,
            CustomerGroupCount = syncTasks.Count,
            Stats = stats
        };
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

        var storeResult = await _redisSafeRunner.ExecuteWithLockAsync(
            $"crm-auto-sync:store:{companyId}:{storeName}",
            async () =>
            {
                var existingStores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: [companyId], cancellationToken: cancellationToken).ConfigureAwait(false);
                var existingStore = existingStores
                    .Where(x => string.Equals(GetStoreName(x.Names), storeName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.CreatedDate)
                    .FirstOrDefault();

                if (existingStore != null)
                {
                    Log.Information("Store reuse after lock. StoreId={StoreId}, StoreName={StoreName}", existingStore.Id, storeName);
                    return new StoreLockResult(existingStore, false);
                }

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
                var createdStore = await _posDataProvider.GetPosCompanyStoreAsync(id: createResponse.Data.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
                createdStore.IsTaskEnabled = true;
                createdStore.Timezone = "America/Los_Angeles";
                await _posDataProvider.UpdatePosCompanyStoresAsync([createdStore], cancellationToken: cancellationToken).ConfigureAwait(false);
                return new StoreLockResult(createdStore, true);
            },
            expiry: TimeSpan.FromMinutes(2),
            wait: TimeSpan.FromSeconds(5),
            retry: TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        store = storeResult.Store;
        if (store == null)
            throw new Exception($"Store create failed. CompanyId={companyId}, StoreName={storeName}");

        storeMap[storeName] = store;
        if (storeResult.IsCreated)
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
                .Where(x => x.Type == AgentType.Sales && x.SourceSystem == AgentSourceSystem.AiResource)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefault(x => string.Equals(x.Name, salesAgentName, StringComparison.OrdinalIgnoreCase))
                ?? existingAgents
                    .Where(x => x.Type == AgentType.Sales && x.SourceSystem == AgentSourceSystem.AiResource)
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
                    SourceSystem = AgentSourceSystem.AiResource,
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
        AiResourceSyncStoreContext storeContext,
        AiResourceSyncAssistantContext assistantContext,
        SourceSceneLookup sourceSceneLookup,
        AiResourceSyncExecutionStatsDto stats,
        CancellationToken cancellationToken)
    {
        var customerAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(mergedGroup.CustomerIds, mergedGroup.Language);
        
        Log.Information(
            "Assistant ensure. Name={AssistantName}, StoreId={StoreId}, AgentId={SalesAgentId}, SalesKey={SalesKey}, Customers={CustomerIds}, Lang={Language}",
            customerAssistantName, storeId, salesAgentId, mergedGroup.SalesKey, string.Join("/", mergedGroup.CustomerIds), mergedGroup.Language ?? "英文");
      
        await ResolveMergedCustomerAssistantAsync(
            serviceProviderId, initiatedByUserId, companyId, storeId, salesAgentId, customerAssistantName, mergedGroup, customerIdLookup,
            storeContext, assistantContext, sourceSceneLookup, stats, cancellationToken).ConfigureAwait(false);

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
        Log.Information("Assistant add request. Name={AssistantName}", customerKnowledgeAssistantName);

        var created = await _mediator.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(new AddAiSpeechAssistantCommand
        {
            ServiceProviderId = serviceProviderId,
            AgentId = salesAgentId,
            CreatedBy = initiatedByUserId,
            AssistantName = customerKnowledgeAssistantName,
            Greetings = _aiResourceSyncSetting.DefaultAssistantGreetings,
            AgentType = AgentType.Sales,
            SourceSystem = AgentSourceSystem.AiResource,
            IsDisplay = true,
            ModelLanguage = language,
            Channels = new List<AiSpeechAssistantChannel> { AiSpeechAssistantChannel.PhoneChat },
            Details = new List<AiSpeechAssistantKnowledgeDetailDto>()
        }, cancellationToken).ConfigureAwait(false);

        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(created.Data.Id, cancellationToken).ConfigureAwait(false);
        await AttachSceneToAssistantKnowledgeAsync(assistant.Id, language, customerIds, sourceSceneLookup, stats, cancellationToken).ConfigureAwait(false);
        customerKnowledgeAssistantCache[assistantCacheKey] = assistant;
        stats.CreatedKnowledgeCount++;
        return assistant;
    }

    private async Task AttachSceneToAssistantKnowledgeAsync(
        int assistantId, string language, IReadOnlyList<string> customerIds, SourceSceneLookup sourceSceneLookup, 
        AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        var scene = ResolveSourceScene(sourceSceneLookup, language);

        if (scene == null)
        {
            Log.Warning(
                "Scene missing. Customers={CustomerIds}, Lang={Language}", string.Join("/", customerIds), language ?? "英文");
            stats.Warnings.Add($"Customer [{string.Join("/", customerIds)}] language [{language ?? "英文"}] has no available source scene mapping yet.");
            return;
        }

        if (!sourceSceneLookup.SceneItems.TryGetValue(scene.Id, out var sceneItems) || sceneItems.Count == 0)
        {
            Log.Warning("Scene empty. SceneId={SceneId}, Customers={CustomerIds}, Lang={Language}",
                scene.Id, string.Join("/", customerIds), language ?? "英文");
            stats.Warnings.Add($"Scene [{scene.Id}] has no items for customer [{string.Join("/", customerIds)}] language [{language ?? "英文"}].");
            return;
        }

        var knowledge = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantKnowledgeAsync(assistantId: assistantId, isActive: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (knowledge == null)
            return;

        var existingRelations = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantKnowledgeSceneRelationsAsync(knowledge.Id, cancellationToken)
            .ConfigureAwait(false);

        var obsoleteCrmRelations = existingRelations
            .Where(x => x.SourceType == AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync && x.SceneId != scene.Id)
            .ToList();

        if (obsoleteCrmRelations.Count > 0)
        {
            await _aiSpeechAssistantDataProvider
                .DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(obsoleteCrmRelations, true, cancellationToken)
                .ConfigureAwait(false);
        }

        if (existingRelations.All(x => x.SceneId != scene.Id))
        {
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeSceneRelationsAsync(
                new List<AiSpeechAssistantKnowledgeSceneRelation>
                {
                    new()
                    {
                        KnowledgeId = knowledge.Id,
                        SceneId = scene.Id,
                        SourceType = AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync,
                        CreatedAt = DateTimeOffset.UtcNow
                    }
                },
                true,
                cancellationToken).ConfigureAwait(false);
        }

        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsAsync([knowledge.Id], cancellationToken).ConfigureAwait(false);
    }

    private async Task ResolveMergedCustomerAssistantAsync(
        int serviceProviderId, int? initiatedByUserId, int companyId, int targetStoreId, int salesAgentId, string assistantName, CrmSalesAutoSyncCustomerGroup mergedGroup,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup, AiResourceSyncStoreContext storeContext,
        AiResourceSyncAssistantContext assistantContext, SourceSceneLookup sourceSceneLookup, AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        Log.Information(
            "Assistant resolve. Name={AssistantName}, StoreId={TargetStoreId}, Customers={CustomerIds}, Lang={Language}",
            assistantName, targetStoreId, string.Join("/", mergedGroup.CustomerIds), mergedGroup.Language ?? "英文");

        assistantContext.ExistingCrmAssistantsByName.TryGetValue(assistantName, out var exactMatch);
        
        if (exactMatch != null)
        {
            Log.Information(
                "Assistant match: exact. AssistantId={AssistantId}, StoreId={CurrentStoreId}, TargetStoreId={TargetStoreId}",
                exactMatch.AssistantId, exactMatch.StoreId, targetStoreId);
            
            assistantContext.ClaimedAssistantIds.Add(exactMatch.AssistantId);
            if (exactMatch.StoreId != targetStoreId || exactMatch.AgentId != salesAgentId)
            {
                await TransferCustomerAssistantToSalesAgentAsync(exactMatch, targetStoreId, salesAgentId, stats, cancellationToken).ConfigureAwait(false);
                exactMatch.StoreId = targetStoreId;
                exactMatch.AgentId = salesAgentId;
                stats.TransferredAssistantCount++;
            }
            
            return;
        }
        
        var desiredIds = mergedGroup.CustomerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sameStoreMatch = FindBestMatchingAssistant(assistantContext.ExistingCrmAssistantsById, assistantContext.AssistantCustomerIdsByAssistantId, assistantContext.AssistantIdsByCustomerId, desiredIds, targetStoreId, assistantContext.ClaimedAssistantIds);
        if (sameStoreMatch != null)
        {
            Log.Information(
                "Assistant match: same-store. AssistantId={AssistantId}, Matched={MatchedAssistantName}, Target={TargetAssistantName}",
                sameStoreMatch.AssistantId, sameStoreMatch.Name, assistantName);
            
            if (assistantContext.AssistantCustomerIdsByAssistantId.TryGetValue(sameStoreMatch.AssistantId, out var existingIds) && existingIds.IsSupersetOf(desiredIds) && !existingIds.SetEquals(desiredIds))
            {
                var removedCustomerIds = existingIds.Except(desiredIds, StringComparer.OrdinalIgnoreCase).ToList();
                Log.Information(
                    "Assistant split needed. AssistantId={AssistantId}, Existing={ExistingCustomerIds}, Desired={DesiredCustomerIds}, Removed={RemovedCustomerIds}",
                    sameStoreMatch.AssistantId, string.Join("/", existingIds), string.Join("/", desiredIds), string.Join("/", removedCustomerIds));
                
                await CopySplitCustomersBeforeShrinkAsync(
                    serviceProviderId, initiatedByUserId, removedCustomerIds,
                    mergedGroup.SalesKey, customerIdLookup, storeContext.StoreMap, companyId, assistantContext.SalesAgentCache, assistantContext.CustomerKnowledgeAssistantCache,
                    sourceSceneLookup, assistantContext.ExistingCrmAssistants, assistantContext.ExistingCrmAssistantsByName, assistantContext.AssistantCustomerIdsByAssistantId, assistantContext.AssistantIdsByCustomerId, assistantContext.ClaimedAssistantIds, stats, cancellationToken).ConfigureAwait(false);
            }

            assistantContext.ClaimedAssistantIds.Add(sameStoreMatch.AssistantId);
            if (!string.Equals(sameStoreMatch.Name, assistantName, StringComparison.OrdinalIgnoreCase))
            {
                await RenameCustomerAssistantAsync(sameStoreMatch, assistantName, stats, cancellationToken).ConfigureAwait(false);
                sameStoreMatch.Name = assistantName;
            }
            
            ReplaceAssistantCustomerMappings(assistantContext.AssistantCustomerIdsByAssistantId, assistantContext.AssistantIdsByCustomerId, sameStoreMatch.AssistantId, desiredIds);
            assistantContext.ExistingCrmAssistantsByName[assistantName] = sameStoreMatch;

            return;
        }

        var crossStoreMatch = FindBestMatchingAssistant(assistantContext.ExistingCrmAssistantsById, assistantContext.AssistantCustomerIdsByAssistantId, assistantContext.AssistantIdsByCustomerId, desiredIds, null, assistantContext.ClaimedAssistantIds);
        if (crossStoreMatch != null)
        {
            if (assistantContext.AssistantCustomerIdsByAssistantId.TryGetValue(crossStoreMatch.AssistantId, out var existingIds) && existingIds.SetEquals(desiredIds))
            {
                Log.Information(
                    "Assistant match: cross-store transfer. AssistantId={AssistantId}, StoreId={CurrentStoreId}, TargetStoreId={TargetStoreId}",
                    crossStoreMatch.AssistantId, crossStoreMatch.StoreId, targetStoreId);
                assistantContext.ClaimedAssistantIds.Add(crossStoreMatch.AssistantId);
                
                await TransferCustomerAssistantToSalesAgentAsync(crossStoreMatch, targetStoreId, salesAgentId, stats, cancellationToken).ConfigureAwait(false);
                crossStoreMatch.StoreId = targetStoreId;
                crossStoreMatch.AgentId = salesAgentId;
                stats.TransferredAssistantCount++;
                
                return;
            }

            if (assistantContext.AssistantCustomerIdsByAssistantId.TryGetValue(crossStoreMatch.AssistantId, out existingIds) && existingIds.IsSupersetOf(desiredIds) && !existingIds.SetEquals(desiredIds))
            {
                Log.Information(
                    "Assistant match: cross-store superset. SourceAssistantId={AssistantId}, Source={MatchedAssistantName}, Target={TargetAssistantName}",
                    crossStoreMatch.AssistantId, crossStoreMatch.Name, assistantName);

                var refreshedGroups = await LoadLatestCustomerGroupsAsync(existingIds, cancellationToken).ConfigureAwait(false);
                var copiedAssistant = await EnsureCustomerKnowledgeAssistantAsync(
                    serviceProviderId, initiatedByUserId, salesAgentId, targetStoreId, assistantName, mergedGroup.Language,
                    mergedGroup.CustomerIds, sourceSceneLookup,
                    assistantContext.CustomerKnowledgeAssistantCache, stats, cancellationToken).ConfigureAwait(false);

                assistantContext.ClaimedAssistantIds.Add(copiedAssistant.Id);
                var copiedAssistantLocation = new CrmAutoSyncAssistantLocationDto
                {
                    AssistantId = copiedAssistant.Id,
                    StoreId = targetStoreId,
                    AgentId = salesAgentId,
                    Name = assistantName
                };
                assistantContext.ExistingCrmAssistants.Add(copiedAssistantLocation);
                assistantContext.ExistingCrmAssistantsByName[assistantName] = copiedAssistantLocation;
                assistantContext.AssistantCustomerIdsByAssistantId[copiedAssistant.Id] = desiredIds;
                AddAssistantIdsByCustomerId(assistantContext.AssistantIdsByCustomerId, desiredIds, copiedAssistant.Id);

                var originalSalesKey = storeContext.ExistingStoreNamesById.TryGetValue(crossStoreMatch.StoreId, out var storeName) ? storeName : null;
                var retainedGroup = SelectRetainedGroup(refreshedGroups, desiredIds, originalSalesKey);
                if (retainedGroup != null)
                {
                    await ReassignExistingAssistantToGroupAsync(
                        serviceProviderId, companyId, initiatedByUserId, crossStoreMatch, retainedGroup, storeContext.StoreMap, assistantContext.SalesAgentCache,
                        stats, cancellationToken).ConfigureAwait(false);
                    
                    ReplaceAssistantCustomerMappings(assistantContext.AssistantCustomerIdsByAssistantId, assistantContext.AssistantIdsByCustomerId, crossStoreMatch.AssistantId,
                        retainedGroup.CustomerIds.ToHashSet(StringComparer.OrdinalIgnoreCase));
                    assistantContext.ExistingCrmAssistantsByName.Remove(crossStoreMatch.Name);
                    crossStoreMatch.Name = CrmSalesAutoSyncGrouping.BuildAssistantName(retainedGroup.CustomerIds, retainedGroup.Language);
                    assistantContext.ExistingCrmAssistantsByName[crossStoreMatch.Name] = crossStoreMatch;
                }
                else
                {
                    Log.Warning("Assistant superset refresh found no retained group. AssistantId={AssistantId}, Existing={ExistingCustomerIds}, Desired={DesiredCustomerIds}",
                        crossStoreMatch.AssistantId, string.Join("/", existingIds), string.Join("/", desiredIds));
                }

                Log.Information("Assistant split created. AssistantId={AssistantId}, Name={AssistantName}, StoreId={StoreId}",
                    copiedAssistant.Id, assistantName, targetStoreId);
                return;
            }
        }

        Log.Information("Assistant match: none. Name={AssistantName}, StoreId={TargetStoreId}", assistantName, targetStoreId);
        
        var createdAssistant = await EnsureCustomerKnowledgeAssistantAsync(
            serviceProviderId, initiatedByUserId, salesAgentId, targetStoreId, assistantName, mergedGroup.Language,
            mergedGroup.CustomerIds, sourceSceneLookup,
            assistantContext.CustomerKnowledgeAssistantCache, stats, cancellationToken).ConfigureAwait(false);
        
        assistantContext.ClaimedAssistantIds.Add(createdAssistant.Id);
        assistantContext.ExistingCrmAssistants.Add(new CrmAutoSyncAssistantLocationDto
        {
            AssistantId = createdAssistant.Id,
            StoreId = targetStoreId,
            Name = assistantName
        });
        assistantContext.ExistingCrmAssistantsByName[assistantName] = assistantContext.ExistingCrmAssistants[^1];
        assistantContext.AssistantCustomerIdsByAssistantId[createdAssistant.Id] = desiredIds;
        AddAssistantIdsByCustomerId(assistantContext.AssistantIdsByCustomerId, desiredIds, createdAssistant.Id);
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
       
        Log.Information("Assistant renamed. AssistantId={AssistantId}, From={FromName}, To={ToName}", assistant.Id, previousAssistantName, assistantName);
    }
    
    private async Task CopySplitCustomersBeforeShrinkAsync(
        int serviceProviderId,
        int? initiatedByUserId,
        IEnumerable<string> removedCustomerIds,
        string currentSalesKey, IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup,
        Dictionary<string, CompanyStore> storeMap,
        int companyId,
        Dictionary<string, Agent> salesAgentCache,
        Dictionary<string, Core.Domain.AISpeechAssistant.AiSpeechAssistant> customerKnowledgeAssistantCache,
        SourceSceneLookup sourceSceneLookup,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        Dictionary<string, CrmAutoSyncAssistantLocationDto> existingCrmAssistantsByName,
        Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId,
        Dictionary<string, HashSet<int>> assistantIdsByCustomerId,
        HashSet<int> claimedAssistantIds,
        AiResourceSyncExecutionStatsDto stats,
        CancellationToken cancellationToken)
    {
        var processedTargetGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removedCustomerIdList = removedCustomerIds.ToList();
        var missingCustomerIds = removedCustomerIdList.Where(x => !customerIdLookup.ContainsKey(x)).ToList();
        var refreshedCustomerLookup = missingCustomerIds.Count == 0
            ? customerIdLookup
            : await BuildLatestCustomerLookupAsync(removedCustomerIdList, customerIdLookup, cancellationToken).ConfigureAwait(false);

        Log.Information(
            "Split start. SalesKey={CurrentSalesKey}, Removed={RemovedCustomerIds}",
            currentSalesKey, string.Join("/", removedCustomerIdList));

        foreach (var removedCustomerId in removedCustomerIdList)
        {
            if (!refreshedCustomerLookup.TryGetValue(removedCustomerId, out var targetGroup))
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
                targetStore = await EnsureSalesStoreAsync(
                    targetGroup.Customers.First(), companyId, storeMap, initiatedByUserId, stats, cancellationToken).ConfigureAwait(false);
            }

            var targetSalesAgent = await EnsureSalesAgentAsync(
                serviceProviderId, targetStore.Id, targetGroup.SalesKey, salesAgentCache, stats, cancellationToken).ConfigureAwait(false);
            
            var targetAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(targetGroup.CustomerIds, targetGroup.Language);
            var copiedAssistant = await EnsureCustomerKnowledgeAssistantAsync(
                serviceProviderId, initiatedByUserId, targetSalesAgent.Id, targetStore.Id, targetAssistantName, 
                targetGroup.Language, targetGroup.CustomerIds, sourceSceneLookup,
                customerKnowledgeAssistantCache, stats, cancellationToken).ConfigureAwait(false);

            claimedAssistantIds.Add(copiedAssistant.Id);
            var copiedAssistantLocation = new CrmAutoSyncAssistantLocationDto
            {
                AssistantId = copiedAssistant.Id,
                StoreId = targetStore.Id,
                AgentId = targetSalesAgent.Id,
                Name = targetAssistantName
            };
            existingCrmAssistants.Add(copiedAssistantLocation);
            existingCrmAssistantsByName[targetAssistantName] = copiedAssistantLocation;
            assistantCustomerIdsByAssistantId[copiedAssistant.Id] = targetGroup.CustomerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            AddAssistantIdsByCustomerId(assistantIdsByCustomerId, targetGroup.CustomerIds, copiedAssistant.Id);

            Log.Information(
                "Split created. AssistantId={AssistantId}, Name={AssistantName}, StoreId={TargetStoreId}, SalesKey={TargetSalesKey}, Customers={CustomerIds}, Lang={Language}",
                copiedAssistant.Id, targetAssistantName, targetStore.Id, targetGroup.SalesKey, string.Join("/", targetGroup.CustomerIds), targetGroup.Language ?? "英文");
        }
    }

    private async Task<IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup>> BuildLatestCustomerLookupAsync(
        IEnumerable<string> customerIds,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> existingLookup,
        CancellationToken cancellationToken)
    {
        var customers = existingLookup.Values
            .SelectMany(x => x.Customers)
            .GroupBy(x => x.CustomerId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var missingCustomerIds = customerIds
            .Where(x => !string.IsNullOrWhiteSpace(x) && !customers.ContainsKey(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var loadedCustomers = await LoadCustomersBySapIdsAsync(missingCustomerIds, cancellationToken).ConfigureAwait(false);
        foreach (var customer in loadedCustomers)
        {
            customers[customer.CustomerId] = customer;
        }

        return CrmSalesAutoSyncGrouping.BuildCustomerIdLookup(CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers.Values));
    }

    private async Task<IReadOnlyList<CrmSalesAutoSyncCustomerGroup>> LoadLatestCustomerGroupsAsync(IEnumerable<string> customerIds, CancellationToken cancellationToken)
    {
        var customers = await LoadCustomersBySapIdsAsync(customerIds, cancellationToken).ConfigureAwait(false);
        return CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);
    }

    private async Task<List<CrmSalesAutoSyncCustomerDto>> LoadCustomersBySapIdsAsync(
        IEnumerable<string> customerIds,
        CancellationToken cancellationToken)
    {
        var distinctCustomerIds = customerIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctCustomerIds.Count == 0)
            return [];

        using var concurrencyLimiter = new SemaphoreSlim(MaxConcurrentCrmCustomerLookups);
        var lookupTasks = distinctCustomerIds.Select(async customerId =>
        {
            await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await _crmClient
                    .GetSalesAutoSyncCustomerBySapIdAsync(customerId, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        });

        var customers = await Task.WhenAll(lookupTasks).ConfigureAwait(false);
        return customers.Where(x => x != null).ToList();
    }

    private static CrmSalesAutoSyncCustomerGroup SelectRetainedGroup(
        IReadOnlyList<CrmSalesAutoSyncCustomerGroup> groups,
        HashSet<string> desiredIds,
        string originalSalesKey)
    {
        return groups
            .Where(x => !x.CustomerIds.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(desiredIds))
            .OrderByDescending(x => string.Equals(x.SalesKey, originalSalesKey, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CustomerIds.Count)
            .FirstOrDefault();
    }

    private async Task ReassignExistingAssistantToGroupAsync(
        int serviceProviderId,
        int companyId,
        int? initiatedByUserId,
        CrmAutoSyncAssistantLocationDto assistantLocation,
        CrmSalesAutoSyncCustomerGroup targetGroup,
        Dictionary<string, CompanyStore> storeMap,
        Dictionary<string, Agent> salesAgentCache,
        AiResourceSyncExecutionStatsDto stats,
        CancellationToken cancellationToken)
    {
        if (!storeMap.TryGetValue(targetGroup.SalesKey, out var targetStore))
        {
            targetStore = await EnsureSalesStoreAsync(
                targetGroup.Customers.First(), companyId, storeMap, initiatedByUserId, stats, cancellationToken).ConfigureAwait(false);
        }

        var targetSalesAgent = await EnsureSalesAgentAsync(
            serviceProviderId, targetStore.Id, targetGroup.SalesKey, salesAgentCache, stats, cancellationToken).ConfigureAwait(false);

        var targetAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(targetGroup.CustomerIds, targetGroup.Language);
        if (assistantLocation.StoreId != targetStore.Id || assistantLocation.AgentId != targetSalesAgent.Id)
        {
            await TransferCustomerAssistantToSalesAgentAsync(
                assistantLocation, targetStore.Id, targetSalesAgent.Id, stats, cancellationToken).ConfigureAwait(false);
            assistantLocation.StoreId = targetStore.Id;
            assistantLocation.AgentId = targetSalesAgent.Id;
        }

        await RenameCustomerAssistantAsync(assistantLocation, targetAssistantName, stats, cancellationToken).ConfigureAwait(false);
    }

    private static void ReplaceAssistantCustomerMappings(
        Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId,
        Dictionary<string, HashSet<int>> assistantIdsByCustomerId,
        int assistantId, HashSet<string> newCustomerIds)
    {
        if (assistantCustomerIdsByAssistantId.TryGetValue(assistantId, out var previousCustomerIds))
        {
            foreach (var previousCustomerId in previousCustomerIds)
            {
                if (!assistantIdsByCustomerId.TryGetValue(previousCustomerId, out var assistantIds))
                    continue;

                assistantIds.Remove(assistantId);
                if (assistantIds.Count == 0)
                    assistantIdsByCustomerId.Remove(previousCustomerId);
            }
        }

        assistantCustomerIdsByAssistantId[assistantId] = newCustomerIds;
        AddAssistantIdsByCustomerId(assistantIdsByCustomerId, newCustomerIds, assistantId);
    }

    private async Task ReconcileInactiveCustomerAssistantsAsync(
        HashSet<string> activeCustomerIds, List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants, Dictionary<int, HashSet<string>> assistantCustomerIdsByAssistantId, HashSet<int> claimedAssistantIds,
        AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        var candidates = existingCrmAssistants
            .Where(x => !claimedAssistantIds.Contains(x.AssistantId))
            .Where(x => assistantCustomerIdsByAssistantId.TryGetValue(x.AssistantId, out var customerIds) && customerIds.Count > 0)
            .ToList();
        if (candidates.Count == 0)
            return;

        var candidateAssistantIds = candidates.Select(x => x.AssistantId).Distinct().ToList();
        var activeKnowledges = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantActiveKnowledgesAsync(candidateAssistantIds, cancellationToken)
            .ConfigureAwait(false) ?? [];
        var assistants = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantsByIdsAsync(candidateAssistantIds, cancellationToken)
            .ConfigureAwait(false) ?? [];
        var activeKnowledgeByAssistantId = activeKnowledges
            .GroupBy(x => x.AssistantId)
            .ToDictionary(x => x.Key, x => x.First());
        var assistantsById = assistants.ToDictionary(x => x.Id);
        var knowledgesToDeactivate = new List<AiSpeechAssistantKnowledge>();
        var assistantsToRename = new List<Core.Domain.AISpeechAssistant.AiSpeechAssistant>();

        foreach (var assistant in existingCrmAssistants.Where(x => !claimedAssistantIds.Contains(x.AssistantId)))
        {
            if (!assistantCustomerIdsByAssistantId.TryGetValue(assistant.AssistantId, out var existingIds) || existingIds.Count == 0)
                continue;

            _ = CrmSalesAutoSyncGrouping.TryParseAssistantName(assistant.Name, out _, out var language);

            var remainingIds = existingIds.Where(activeCustomerIds.Contains).ToList();

            if (remainingIds.Count == 0)
            {
                if (activeKnowledgeByAssistantId.TryGetValue(assistant.AssistantId, out var activeKnowledge))
                {
                    activeKnowledge.IsActive = false;
                    knowledgesToDeactivate.Add(activeKnowledge);
                }
                else
                {
                    Log.Information("Knowledge deactivate skip. AssistantId={AssistantId}", assistant.AssistantId);
                }

                stats.DeactivatedAssistantCount++;
                RecordDeactivatedAssistant(stats, assistant.AssistantId, assistant.StoreId, assistant.AgentId, assistant.Name);
                stats.Warnings.Add($"Assistant [{assistant.Name}] has no active CRM customers remaining; knowledge deactivated.");
                Log.Information("Assistant deactivate. AssistantId={AssistantId}, Name={AssistantName}", assistant.AssistantId, assistant.Name);
                continue;
            }

            if (remainingIds.Count == existingIds.Count)
                continue;

            var renamedAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(remainingIds, language);
            Log.Information("Assistant shrink. AssistantId={AssistantId}, From={FromName}, To={ToName}", assistant.AssistantId, assistant.Name, renamedAssistantName);

            if (assistantsById.TryGetValue(assistant.AssistantId, out var assistantToRename) &&
                !string.Equals(assistantToRename.Name, renamedAssistantName, StringComparison.OrdinalIgnoreCase))
            {
                var previousAssistantName = assistantToRename.Name;
                assistantToRename.Name = renamedAssistantName;
                assistantsToRename.Add(assistantToRename);
                RecordRenamedAssistant(stats, assistantToRename.Id, assistant.StoreId, assistant.AgentId, renamedAssistantName, previousAssistantName);
            }
            
            assistant.Name = renamedAssistantName;
            assistantCustomerIdsByAssistantId[assistant.AssistantId] = remainingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (knowledgesToDeactivate.Count > 0)
        {
            await _aiSpeechAssistantDataProvider
                .UpdateAiSpeechAssistantKnowledgesAsync(knowledgesToDeactivate, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (var knowledge in knowledgesToDeactivate)
                Log.Information("Knowledge deactivated. AssistantId={AssistantId}, KnowledgeId={KnowledgeId}", knowledge.AssistantId, knowledge.Id);
        }

        if (assistantsToRename.Count > 0)
        {
            await _aiSpeechAssistantDataProvider
                .UpdateAiSpeechAssistantsAsync(assistantsToRename, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task TransferCustomerAssistantToSalesAgentAsync(
        CrmAutoSyncAssistantLocationDto assistantLocation, int targetStoreId, int targetAgentId, AiResourceSyncExecutionStatsDto stats, CancellationToken cancellationToken)
    {
        if (targetAgentId <= 0)
            throw new Exception($"Target sales agent is required. AssistantId={assistantLocation.AssistantId}");

        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(assistantLocation.AssistantId, cancellationToken).ConfigureAwait(false);
        if (assistant == null)
            throw new Exception($"Assistant not found. AssistantId={assistantLocation.AssistantId}");

        var previousStoreId = assistantLocation.StoreId;
        var previousAgentId = assistant.AgentId;
        var agentAssistants = await _aiSpeechAssistantDataProvider.GetAgentAssistantsAsync(assistantIds: [assistant.Id], cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

        if (agentAssistants.Count > 0)
        {
            await _aiSpeechAssistantDataProvider.DeleteAgentAssistantsAsync(agentAssistants, true, cancellationToken).ConfigureAwait(false);
        }

        await _aiSpeechAssistantDataProvider.AddAgentAssistantsAsync(
            [new AgentAssistant { AgentId = targetAgentId, AssistantId = assistant.Id }],
            true, cancellationToken).ConfigureAwait(false);

        if (assistant.AgentId != targetAgentId)
        {
            assistant.AgentId = targetAgentId;
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], true, cancellationToken).ConfigureAwait(false);
        }

        RecordTransferredAssistant(stats, assistant.Id, targetStoreId, targetAgentId, assistant.Name, previousStoreId);
        Log.Information(
            "Assistant transferred to sales agent. AssistantId={AssistantId}, FromStoreId={FromStoreId}, ToStoreId={ToStoreId}, FromAgentId={FromAgentId}, ToAgentId={ToAgentId}",
            assistant.Id, previousStoreId, targetStoreId, previousAgentId, targetAgentId);
    }
    
    private async Task<SourceSceneLookup> BuildSourceSceneLookupAsync(int companyId, CancellationToken cancellationToken)
    {
        var mappings = await _knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(
            companyId: companyId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var mappingSceneIds = mappings
            .GroupBy(x => x.Language)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.CreatedAt).First().SceneId);

        if (mappingSceneIds.Count == 0)
            return new SourceSceneLookup
            {
                MappingScenes = new Dictionary<AutoAddLanguage, KnowledgeScene>(),
                SceneItems = new Dictionary<int, List<KnowledgeSceneItem>>()
            };

        var scenes = await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(mappingSceneIds.Values.Distinct().ToList(), cancellationToken).ConfigureAwait(false);
       
        var sceneMap = scenes.ToDictionary(x => x.Id);
        var mappingScenes = mappingSceneIds
            .Where(x => sceneMap.ContainsKey(x.Value))
            .ToDictionary(x => x.Key, x => sceneMap[x.Value]);
        var sceneIds = mappingScenes.Values.Select(x => x.Id).Distinct().ToList();
        var sceneItems = await _knowledgeScenarioDataProvider
                .GetKnowledgeSceneItemsBySceneIdsAsync(sceneIds, cancellationToken)
                .ConfigureAwait(false) ?? [];
        var sceneItemsLookup = sceneItems
            .GroupBy(x => x.SceneId)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var sceneId in sceneIds)
            sceneItemsLookup.TryAdd(sceneId, []);

        return new SourceSceneLookup
        {
            MappingScenes = mappingScenes,
            SceneItems = sceneItemsLookup
        };
    }

    private KnowledgeScene ResolveSourceScene(SourceSceneLookup sourceSceneLookup, string language)
    {
        var rawLanguage = string.IsNullOrWhiteSpace(language) ? "英文" : language;
        var normalizedLanguage = CrmToAutoAddLanguageConverter.TryResolve(rawLanguage, out var resolvedLanguage) ? resolvedLanguage : AutoAddLanguage.English;

        return sourceSceneLookup.MappingScenes.TryGetValue(normalizedLanguage, out var scene) ? scene : null;
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
            Mode = isInitialRelease ? "initial" : command.IsManual && command.IsFullSync ? "manual_full" : command.IsManual ? "manual" : "automatic",
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

    public async Task SendNotifyAsync(bool isSuccess, bool isManual, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_aiResourceSyncSetting.NotifyRobotUrl))
            return;

        var pstZone = PstTimeZone.Get();
        var currentTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, pstZone).ToString("yyyy-MM-dd HH:mm:ss");

        var content = isManual
            ? isSuccess
                ? $"✅SMT Sales Manual Create: Success\nTime: {currentTime}"
                : $"❌SMT Sales Manual Create: Failed\nTime: {currentTime}"
            : isSuccess
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
