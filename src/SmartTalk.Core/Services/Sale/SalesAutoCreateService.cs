using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Commands.Sales;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesAutoCreateService
{
    Task<SyncCrmSalesAutoCreateResponse> SyncCrmSalesAutoCreateAsync(SyncCrmSalesAutoCreateCommand command, CancellationToken cancellationToken);
}

public class SalesAutoCreateService : ISalesAutoCreateService
{
    private const int MaxSyncAttempts = 3;
    private const int RetryDelaySeconds = 300;

    private static readonly TimeZoneInfo PacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    private const string DefaultKnowledgeGreetings = "Hello, how can I help you today?";

    private readonly IMediator _mediator;
    private readonly ICrmClient _crmClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IKnowledgeScenarioDataProvider _knowledgeScenarioDataProvider;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly IWeChatClient _weChatClient;
    private readonly SalesSetting _salesSetting;
    private readonly SalesAutoCreateSetting _salesAutoCreateSetting;

    public SalesAutoCreateService(
        IMediator mediator,
        ICrmClient crmClient,
        IPosDataProvider posDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IKnowledgeScenarioDataProvider knowledgeScenarioDataProvider,
        ISalesDataProvider salesDataProvider,
        IWeChatClient weChatClient,
        SalesSetting salesSetting,
        SalesAutoCreateSetting salesAutoCreateSetting)
    {
        _mediator = mediator;
        _crmClient = crmClient;
        _posDataProvider = posDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
        _salesDataProvider = salesDataProvider;
        _weChatClient = weChatClient;
        _salesSetting = salesSetting;
        _salesAutoCreateSetting = salesAutoCreateSetting;
    }

    public async Task<SyncCrmSalesAutoCreateResponse> SyncCrmSalesAutoCreateAsync(SyncCrmSalesAutoCreateCommand command, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 1; attempt <= MaxSyncAttempts; attempt++)
        {
            try
            {
                var response = await SyncInternalAsync(command, cancellationToken).ConfigureAwait(false);

                await RecordSyncRunAsync(command, response.Data, true, null, cancellationToken).ConfigureAwait(false);

                if (!command.IsManual && attempt > 1)
                    await SendNotifyAsync(true, cancellationToken).ConfigureAwait(false);

                return response;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log.Warning(ex, "SyncCrmSalesAutoCreate attempt {Attempt}/{MaxAttempts} failed", attempt, MaxSyncAttempts);

                if (attempt < MaxSyncAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                Log.Error(ex, "SyncCrmSalesAutoCreate failed after {MaxAttempts} attempts", MaxSyncAttempts);
                await RecordSyncRunAsync(command, null, false, ex.Message, cancellationToken).ConfigureAwait(false);

                if (!command.IsManual)
                    await SendNotifyAsync(false, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException!;
    }

    private async Task<SyncCrmSalesAutoCreateResponse> SyncInternalAsync(
        SyncCrmSalesAutoCreateCommand command, CancellationToken cancellationToken)
    {
        var customers = await _crmClient.GetSalesAutoSyncCustomersAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var customerGroups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(customers);
        var customerIdLookup = CrmSalesAutoSyncGrouping.BuildCustomerIdLookup(customerGroups);
        var activeCustomerIds = CrmSalesAutoSyncGrouping.BuildActiveCustomerIds(customers);
        var result = new SyncCrmSalesAutoCreateResponseData { TotalCount = customers.Count };

        var company = await _posDataProvider.GetPosCompanyByNameAsync(_salesSetting.CompanyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
            throw new Exception($"Sales company [{_salesSetting.CompanyName}] not found.");

        result.IsInitialRelease = !command.IsManual
            && !await _aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(company.Id, cancellationToken).ConfigureAwait(false);

        var existingCrmAssistants = await _aiSpeechAssistantDataProvider
            .GetCrmAutoSyncAssistantsInCompanyAsync(company.Id, cancellationToken)
            .ConfigureAwait(false);
        var claimedAssistantIds = new HashSet<int>();

        var storeNames = customerGroups.Select(x => x.SalesKey).Distinct().ToList();
        var existingStores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);

        var storeMap = existingStores
            .Select(x => new { Store = x, StoreName = GetStoreName(x.Names) })
            .Where(x => !string.IsNullOrWhiteSpace(x.StoreName) && storeNames.Contains(x.StoreName))
            .GroupBy(x => x.StoreName)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Store.CreatedDate).First().Store);

        foreach (var salesBucket in customerGroups.GroupBy(x => x.SalesKey))
        {
            var seedCustomer = salesBucket.First().Customers.First();
            var storeName = salesBucket.Key;
            var primaryLanguage = salesBucket
                .Select(x => x.Language)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "English";

            var store = await EnsureSalesStoreAsync(seedCustomer, company.Id, storeMap, result, cancellationToken).ConfigureAwait(false);
            await EnsureSalesAssistantAsync(command, store.Id, storeName, primaryLanguage, result, cancellationToken).ConfigureAwait(false);
        }

        foreach (var salesBucket in customerGroups.GroupBy(x => x.SalesKey))
        {
            if (!storeMap.TryGetValue(salesBucket.Key, out var store))
                continue;

            foreach (var mergedGroup in salesBucket)
            {
                await EnsureMergedCustomerKnowledgeAsync(
                    command,
                    company.Id,
                    store.Id,
                    mergedGroup,
                    customerIdLookup,
                    storeMap,
                    existingCrmAssistants,
                    claimedAssistantIds,
                    result,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await ReconcileInactiveCustomerAssistantsAsync(
            activeCustomerIds,
            existingCrmAssistants,
            claimedAssistantIds,
            result,
            cancellationToken).ConfigureAwait(false);

        return new SyncCrmSalesAutoCreateResponse
        {
            Data = result
        };
    }

    private async Task<Core.Domain.Pos.CompanyStore> EnsureSalesStoreAsync(
        CrmSalesAutoSyncCustomerDto customer, int companyId, Dictionary<string, Core.Domain.Pos.CompanyStore> storeMap,
        SyncCrmSalesAutoCreateResponseData result, CancellationToken cancellationToken)
    {
        var storeName = CrmSalesAutoSyncGrouping.BuildSalesKey(customer);
        if (storeMap.TryGetValue(storeName, out var store))
            return store;

        var createResponse = await _mediator.SendAsync<CreateCompanyStoreCommand, CreateCompanyStoreResponse>(new CreateCompanyStoreCommand
        {
            CompanyId = companyId,
            Names = BuildStoreNamesJson(storeName),
            Description = $"Auto created from CRM sync for {customer.SalesName}",
            PhoneNumbers = new List<string>()
        }, cancellationToken).ConfigureAwait(false);

        result.CreatedStoreCount++;
        store = await _posDataProvider.GetPosCompanyStoreAsync(id: createResponse.Data.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        storeMap[storeName] = store;
        return store;
    }

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> EnsureSalesAssistantAsync(
        SyncCrmSalesAutoCreateCommand command, int storeId, string assistantName, string language,
        SyncCrmSalesAutoCreateResponseData result, CancellationToken cancellationToken)
    {
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByStoreIdAsync(storeId, cancellationToken).ConfigureAwait(false);
        var assistant = assistants.FirstOrDefault(x => x.Name == assistantName);
        if (assistant != null)
            return assistant;

        var created = await _mediator.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(new AddAiSpeechAssistantCommand
        {
            ServiceProviderId = command.ServiceProviderId,
            StoreId = storeId,
            AssistantName = assistantName,
            Greetings = _salesAutoCreateSetting.DefaultAssistantGreetings,
            AgentType = AgentType.Sales,
            SourceSystem = AgentSourceSystem.CrmAutoSync,
            ModelLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim(),
            Channels = new List<AiSpeechAssistantChannel> { AiSpeechAssistantChannel.PhoneChat },
            Details = new List<AiSpeechAssistantKnowledgeDetailDto>()
        }, cancellationToken).ConfigureAwait(false);

        result.CreatedAssistantCount++;
        return await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(created.Data.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> EnsureCustomerKnowledgeAssistantAsync(
        SyncCrmSalesAutoCreateCommand command,
        int storeId,
        string assistantName,
        string language,
        SyncCrmSalesAutoCreateResponseData result,
        CancellationToken cancellationToken)
    {
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByStoreIdAsync(storeId, cancellationToken).ConfigureAwait(false);
        var assistant = assistants.FirstOrDefault(x => x.Name == assistantName);
        if (assistant != null)
            return assistant;

        var created = await _mediator.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(new AddAiSpeechAssistantCommand
        {
            ServiceProviderId = command.ServiceProviderId,
            StoreId = storeId,
            AssistantName = assistantName,
            Greetings = _salesAutoCreateSetting.DefaultAssistantGreetings,
            AgentType = AgentType.Assistant,
            SourceSystem = AgentSourceSystem.CrmAutoSync,
            IsDisplay = false,
            ModelLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim(),
            Channels = new List<AiSpeechAssistantChannel> { AiSpeechAssistantChannel.PhoneChat },
            Details = new List<AiSpeechAssistantKnowledgeDetailDto>()
        }, cancellationToken).ConfigureAwait(false);

        result.CreatedAssistantCount++;
        return await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(created.Data.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> EnsureMergedCustomerKnowledgeAsync(
        SyncCrmSalesAutoCreateCommand command,
        int companyId,
        int storeId,
        CrmSalesAutoSyncCustomerGroup mergedGroup,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup,
        Dictionary<string, Core.Domain.Pos.CompanyStore> storeMap,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        HashSet<int> claimedAssistantIds,
        SyncCrmSalesAutoCreateResponseData result,
        CancellationToken cancellationToken)
    {
        var customerAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(mergedGroup.CustomerIds, mergedGroup.Language);
        var seedCustomer = mergedGroup.Customers.First();
        var customerAssistant = await ResolveMergedCustomerAssistantAsync(
            command,
            companyId,
            storeId,
            customerAssistantName,
            mergedGroup,
            customerIdLookup,
            storeMap,
            existingCrmAssistants,
            claimedAssistantIds,
            result,
            cancellationToken).ConfigureAwait(false);

        var activeKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            assistantId: customerAssistant.Id,
            isActive: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (activeKnowledge == null)
        {
            await _mediator.SendAsync<AddAiSpeechAssistantKnowledgeCommand, AddAiSpeechAssistantKnowledgeResponse>(new AddAiSpeechAssistantKnowledgeCommand
            {
                AssistantId = customerAssistant.Id,
                Greetings = _salesAutoCreateSetting.DefaultAssistantGreetings ?? DefaultKnowledgeGreetings,
                Json = "{}",
                Language = mergedGroup.Language,
                Premise = $"CRM Customer Knowledge {string.Join("/", mergedGroup.CustomerIds)}",
                Details = new List<AiSpeechAssistantKnowledgeDetailDto>()
            }, cancellationToken).ConfigureAwait(false);

            result.CreatedKnowledgeCount++;

            activeKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
                assistantId: customerAssistant.Id,
                isActive: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await ApplySourceScenesToCustomerAssistantAsync(
            storeId,
            customerAssistant.Id,
            seedCustomer,
            mergedGroup.CustomerIds,
            result,
            cancellationToken).ConfigureAwait(false);

        return customerAssistant;
    }

    private async Task ApplySourceScenesToCustomerAssistantAsync(
        int storeId,
        int assistantId,
        CrmSalesAutoSyncCustomerDto seedCustomer,
        IReadOnlyList<string> customerIds,
        SyncCrmSalesAutoCreateResponseData result,
        CancellationToken cancellationToken)
    {
        var sourceSceneIds = await ResolveSourceSceneIdsAsync(storeId, seedCustomer, cancellationToken).ConfigureAwait(false);
        if (sourceSceneIds.Count == 0)
        {
            result.Warnings.Add(
                $"Customer [{string.Join("/", customerIds)}] language [{seedCustomer.Language ?? "English"}] has no source scene mapping yet.");
            return;
        }

        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (store == null)
            return;

        foreach (var sceneId in sourceSceneIds)
        {
            var scene = (await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync([sceneId], cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            if (scene == null || scene.Status != KnowledgeSceneStatus.Published)
            {
                result.Warnings.Add($"Scene [{sceneId}] is unavailable for customer [{string.Join("/", customerIds)}].");
                continue;
            }

            await _mediator.SendAsync<UpdateKnowledgeSceneCompanyCommand, UpdateKnowledgeSceneCompanyResponse>(new UpdateKnowledgeSceneCompanyCommand
            {
                SceneId = sceneId,
                CompanyId = store.CompanyId,
                StoreId = storeId,
                IsApplied = true
            }, cancellationToken).ConfigureAwait(false);

            await _mediator.SendAsync<SaveKnowledgeSceneRelatedKnowledgesCommand, SaveKnowledgeSceneRelatedKnowledgesResponse>(new SaveKnowledgeSceneRelatedKnowledgesCommand
            {
                SceneId = sceneId,
                StoreId = storeId,
                AssistantIds = new List<int> { assistantId }
            }, cancellationToken).ConfigureAwait(false);

            result.AppliedSceneCount++;
        }
    }

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> ResolveMergedCustomerAssistantAsync(
        SyncCrmSalesAutoCreateCommand command,
        int companyId,
        int targetStoreId,
        string assistantName,
        CrmSalesAutoSyncCustomerGroup mergedGroup,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup,
        Dictionary<string, Core.Domain.Pos.CompanyStore> storeMap,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        HashSet<int> claimedAssistantIds,
        SyncCrmSalesAutoCreateResponseData result,
        CancellationToken cancellationToken)
    {
        var exactMatch = existingCrmAssistants.FirstOrDefault(x =>
            string.Equals(x.Name, assistantName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            claimedAssistantIds.Add(exactMatch.AssistantId);
            if (exactMatch.StoreId != targetStoreId)
            {
                await TransferCustomerAssistantToStoreAsync(exactMatch, targetStoreId, cancellationToken).ConfigureAwait(false);
                exactMatch.StoreId = targetStoreId;
                result.TransferredAssistantCount++;
            }

            return await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantByIdAsync(exactMatch.AssistantId, cancellationToken)
                .ConfigureAwait(false);
        }

        var desiredIds = mergedGroup.CustomerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sameStoreMatch = FindBestMatchingAssistant(existingCrmAssistants, desiredIds, targetStoreId, claimedAssistantIds);
        if (sameStoreMatch != null)
        {
            if (TryParseAssistantIds(sameStoreMatch.Name, out var existingIds)
                && existingIds.IsSupersetOf(desiredIds)
                && !existingIds.SetEquals(desiredIds))
            {
                await CopySplitCustomersBeforeShrinkAsync(
                    command,
                    existingIds.Except(desiredIds, StringComparer.OrdinalIgnoreCase),
                    sameStoreMatch,
                    mergedGroup.SalesKey,
                    customerIdLookup,
                    storeMap,
                    existingCrmAssistants,
                    claimedAssistantIds,
                    result,
                    cancellationToken).ConfigureAwait(false);
            }

            claimedAssistantIds.Add(sameStoreMatch.AssistantId);
            if (!string.Equals(sameStoreMatch.Name, assistantName, StringComparison.OrdinalIgnoreCase))
            {
                await RenameCustomerAssistantAsync(sameStoreMatch, assistantName, cancellationToken).ConfigureAwait(false);
                sameStoreMatch.Name = assistantName;
            }

            return await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantByIdAsync(sameStoreMatch.AssistantId, cancellationToken)
                .ConfigureAwait(false);
        }

        var crossStoreMatch = FindBestMatchingAssistant(existingCrmAssistants, desiredIds, storeId: null, claimedAssistantIds);
        if (crossStoreMatch != null)
        {
            if (TryParseAssistantIds(crossStoreMatch.Name, out var existingIds)
                && existingIds.SetEquals(desiredIds))
            {
                claimedAssistantIds.Add(crossStoreMatch.AssistantId);
                await TransferCustomerAssistantToStoreAsync(crossStoreMatch, targetStoreId, cancellationToken).ConfigureAwait(false);
                crossStoreMatch.StoreId = targetStoreId;
                result.TransferredAssistantCount++;

                return await _aiSpeechAssistantDataProvider
                    .GetAiSpeechAssistantByIdAsync(crossStoreMatch.AssistantId, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (TryParseAssistantIds(crossStoreMatch.Name, out existingIds)
                && existingIds.IsSupersetOf(desiredIds)
                && !existingIds.SetEquals(desiredIds))
            {
                var copiedAssistant = await EnsureCustomerKnowledgeAssistantAsync(
                    command, targetStoreId, assistantName, mergedGroup.Language, result, cancellationToken).ConfigureAwait(false);
                await CopyCustomerKnowledgeAsync(
                    crossStoreMatch.AssistantId,
                    copiedAssistant.Id,
                    targetStoreId,
                    mergedGroup,
                    result,
                    cancellationToken).ConfigureAwait(false);
                claimedAssistantIds.Add(copiedAssistant.Id);
                existingCrmAssistants.Add(new CrmAutoSyncAssistantLocationDto
                {
                    AssistantId = copiedAssistant.Id,
                    StoreId = targetStoreId,
                    Name = assistantName
                });
                return copiedAssistant;
            }
        }

        var createdAssistant = await EnsureCustomerKnowledgeAssistantAsync(command, targetStoreId, assistantName, mergedGroup.Language, result, cancellationToken)
            .ConfigureAwait(false);
        claimedAssistantIds.Add(createdAssistant.Id);
        existingCrmAssistants.Add(new CrmAutoSyncAssistantLocationDto
        {
            AssistantId = createdAssistant.Id,
            StoreId = targetStoreId,
            Name = assistantName
        });
        return createdAssistant;
    }

    private static CrmAutoSyncAssistantLocationDto FindBestMatchingAssistant(
        IEnumerable<CrmAutoSyncAssistantLocationDto> assistants,
        HashSet<string> desiredIds,
        int? storeId,
        HashSet<int> claimedAssistantIds)
    {
        return assistants
            .Where(x => !claimedAssistantIds.Contains(x.AssistantId))
            .Where(x => storeId == null || x.StoreId == storeId)
            .Select(x => new
            {
                Assistant = x,
                ExistingIds = TryParseAssistantIds(x.Name, out var ids) ? ids : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            })
            .Where(x => x.ExistingIds.Overlaps(desiredIds))
            .OrderByDescending(x => x.ExistingIds.Intersect(desiredIds).Count())
            .ThenByDescending(x => x.ExistingIds.SetEquals(desiredIds))
            .Select(x => x.Assistant)
            .FirstOrDefault();
    }

    private static bool TryParseAssistantIds(string assistantName, out HashSet<string> customerIds)
    {
        customerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!CrmSalesAutoSyncGrouping.TryParseAssistantName(assistantName, out var ids, out _))
            return false;

        customerIds = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return customerIds.Count > 0;
    }

    private async Task RenameCustomerAssistantAsync(
        CrmAutoSyncAssistantLocationDto assistantLocation, string assistantName, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantByIdAsync(assistantLocation.AssistantId, cancellationToken)
            .ConfigureAwait(false);
        if (assistant == null || string.Equals(assistant.Name, assistantName, StringComparison.OrdinalIgnoreCase))
            return;

        assistant.Name = assistantName;
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task CopyCustomerKnowledgeAsync(
        int sourceAssistantId,
        int targetAssistantId,
        int targetStoreId,
        CrmSalesAutoSyncCustomerGroup mergedGroup,
        SyncCrmSalesAutoCreateResponseData result,
        CancellationToken cancellationToken)
    {
        var existingKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            assistantId: targetAssistantId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        var knowledgeCreated = false;

        if (existingKnowledge == null)
        {
            var sourceKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
                assistantId: sourceAssistantId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (sourceKnowledge == null)
            {
                await ApplySourceScenesToCustomerAssistantAsync(
                    targetStoreId,
                    targetAssistantId,
                    mergedGroup.Customers.First(),
                    mergedGroup.CustomerIds,
                    result,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var details = await _aiSpeechAssistantDataProvider
                .GetKnowledgeDetailsByKnowledgeIdAsync(sourceKnowledge.Id, cancellationToken)
                .ConfigureAwait(false);

            await _mediator.SendAsync<AddAiSpeechAssistantKnowledgeCommand, AddAiSpeechAssistantKnowledgeResponse>(new AddAiSpeechAssistantKnowledgeCommand
            {
                AssistantId = targetAssistantId,
                Greetings = sourceKnowledge.Greetings ?? _salesAutoCreateSetting.DefaultAssistantGreetings ?? DefaultKnowledgeGreetings,
                Json = sourceKnowledge.Json ?? "{}",
                Language = mergedGroup.Language,
                Premise = $"CRM Customer Knowledge {string.Join("/", mergedGroup.CustomerIds)}",
                Details = details.Select(x => new AiSpeechAssistantKnowledgeDetailDto
                {
                    KnowledgeName = x.KnowledgeName,
                    FormatType = x.FormatType,
                    Content = x.Content,
                    FileName = x.FileName
                }).ToList()
            }, cancellationToken).ConfigureAwait(false);

            result.CreatedKnowledgeCount++;
            knowledgeCreated = true;
        }

        await ApplySourceScenesToCustomerAssistantAsync(
            targetStoreId,
            targetAssistantId,
            mergedGroup.Customers.First(),
            mergedGroup.CustomerIds,
            result,
            cancellationToken).ConfigureAwait(false);

        if (knowledgeCreated)
            return;

        var activeKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            assistantId: targetAssistantId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (activeKnowledge != null)
            return;

        result.Warnings.Add(
            $"Customer [{string.Join("/", mergedGroup.CustomerIds)}] copied assistant [{targetAssistantId}] has no active knowledge after scene sync.");
    }

    private async Task CopySplitCustomersBeforeShrinkAsync(
        SyncCrmSalesAutoCreateCommand command,
        IEnumerable<string> removedCustomerIds,
        CrmAutoSyncAssistantLocationDto sourceAssistant,
        string currentSalesKey,
        IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> customerIdLookup,
        Dictionary<string, Core.Domain.Pos.CompanyStore> storeMap,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        HashSet<int> claimedAssistantIds,
        SyncCrmSalesAutoCreateResponseData result,
        CancellationToken cancellationToken)
    {
        var processedTargetGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var removedCustomerId in removedCustomerIds)
        {
            if (!customerIdLookup.TryGetValue(removedCustomerId, out var targetGroup))
                continue;

            if (string.Equals(targetGroup.SalesKey, currentSalesKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var targetGroupKey = $"{targetGroup.SalesKey}|{CrmSalesAutoSyncGrouping.BuildAssistantName(targetGroup.CustomerIds, targetGroup.Language)}";
            if (!processedTargetGroups.Add(targetGroupKey))
                continue;

            if (!storeMap.TryGetValue(targetGroup.SalesKey, out var targetStore))
                continue;

            var targetAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(targetGroup.CustomerIds, targetGroup.Language);
            var copiedAssistant = await EnsureCustomerKnowledgeAssistantAsync(
                command, targetStore.Id, targetAssistantName, targetGroup.Language, result, cancellationToken).ConfigureAwait(false);

            await CopyCustomerKnowledgeAsync(
                sourceAssistant.AssistantId,
                copiedAssistant.Id,
                targetStore.Id,
                targetGroup,
                result,
                cancellationToken).ConfigureAwait(false);

            claimedAssistantIds.Add(copiedAssistant.Id);
            existingCrmAssistants.Add(new CrmAutoSyncAssistantLocationDto
            {
                AssistantId = copiedAssistant.Id,
                StoreId = targetStore.Id,
                Name = targetAssistantName
            });
        }
    }

    private async Task ReconcileInactiveCustomerAssistantsAsync(
        HashSet<string> activeCustomerIds,
        List<CrmAutoSyncAssistantLocationDto> existingCrmAssistants,
        HashSet<int> claimedAssistantIds,
        SyncCrmSalesAutoCreateResponseData result,
        CancellationToken cancellationToken)
    {
        foreach (var assistant in existingCrmAssistants.Where(x => !claimedAssistantIds.Contains(x.AssistantId)))
        {
            if (!CrmSalesAutoSyncGrouping.TryParseAssistantName(assistant.Name, out var existingIds, out var language))
                continue;

            var remainingIds = existingIds
                .Where(activeCustomerIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (remainingIds.Count == 0)
            {
                await DeactivateCustomerAssistantKnowledgeAsync(assistant.AssistantId, cancellationToken).ConfigureAwait(false);
                result.DeactivatedAssistantCount++;
                result.Warnings.Add($"Assistant [{assistant.Name}] has no active CRM customers remaining; knowledge deactivated.");
                continue;
            }

            if (remainingIds.Count == existingIds.Count)
                continue;

            var renamedAssistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(remainingIds, language);
            await RenameCustomerAssistantAsync(assistant, renamedAssistantName, cancellationToken).ConfigureAwait(false);
            assistant.Name = renamedAssistantName;
        }
    }

    private async Task DeactivateCustomerAssistantKnowledgeAsync(int assistantId, CancellationToken cancellationToken)
    {
        var activeKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            assistantId: assistantId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (activeKnowledge == null)
            return;

        activeKnowledge.IsActive = false;
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync([activeKnowledge], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public static bool IsSamePacificDay(DateTimeOffset left, DateTimeOffset right)
    {
        var leftDate = TimeZoneInfo.ConvertTime(left, PacificTimeZone).Date;
        var rightDate = TimeZoneInfo.ConvertTime(right, PacificTimeZone).Date;
        return leftDate == rightDate;
    }

    public static bool ShouldSkipAutomaticSyncForInitialReleaseDay(DateTimeOffset? latestInitialReleaseCreatedDate, DateTimeOffset now)
    {
        return latestInitialReleaseCreatedDate.HasValue
            && IsSamePacificDay(latestInitialReleaseCreatedDate.Value, now);
    }

    private async Task TransferCustomerAssistantToStoreAsync(
        CrmAutoSyncAssistantLocationDto assistantLocation, int targetStoreId, CancellationToken cancellationToken)
    {
        var posAgent = await _posDataProvider.GetPosAgentByAgentIdAsync(assistantLocation.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (posAgent == null || posAgent.StoreId == targetStoreId)
            return;

        posAgent.StoreId = targetStoreId;
        await _posDataProvider.UpdatePosAgentsAsync([posAgent], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<int>> ResolveSourceSceneIdsAsync(int storeId, CrmSalesAutoSyncCustomerDto customer, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (store == null)
            return new List<int>();

        var sceneCompanies = await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompaniesAsync(companyId: store.CompanyId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (sceneCompanies.Count == 0)
            return new List<int>();

        var sceneIds = sceneCompanies
            .Where(x => x.StoreId == null)
            .Select(x => x.SceneId)
            .Distinct()
            .ToList();

        var language = CrmToAutoAddLanguageConverter.ResolveLanguageCodeOrDefault(
            string.IsNullOrWhiteSpace(customer.Language) ? "English" : customer.Language);
        var mappings = await _knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(
            companyId: store.CompanyId,
            sceneIds: sceneIds,
            language: language,
            isActive: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (mappings.Count > 0)
            return mappings.Select(x => x.SceneId).Distinct().ToList();

        var scenes = await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
        return scenes
            .Where(x => x.Status == KnowledgeSceneStatus.Published
                && x.Name.Contains(language, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToList();
    }

    private async Task RecordSyncRunAsync(SyncCrmSalesAutoCreateCommand command, SyncCrmSalesAutoCreateResponseData result, bool isSuccess, string errorMessage, CancellationToken cancellationToken)
    {
        var run = new CrmSalesAutoSyncRun
        {
            Mode = result?.IsInitialRelease == true ? "initial" : command.IsManual ? "manual" : "automatic",
            IsSuccess = isSuccess,
            TotalCount = result?.TotalCount ?? 0,
            CreatedStoreCount = result?.CreatedStoreCount ?? 0,
            CreatedAssistantCount = result?.CreatedAssistantCount ?? 0,
            CreatedKnowledgeCount = result?.CreatedKnowledgeCount ?? 0,
            AppliedSceneCount = result?.AppliedSceneCount ?? 0,
            WarningsJson = result == null ? null : JsonConvert.SerializeObject(result.Warnings),
            ErrorMessage = errorMessage
        };

        await _salesDataProvider.AddCrmSalesAutoSyncRunAsync(run, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task SendNotifyAsync(bool isSuccess, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_salesAutoCreateSetting.NotifyRobotUrl))
            return;

        var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.Now.UtcDateTime, PacificTimeZone).ToString("yyyy-MM-dd HH:mm:ss");
        var content = isSuccess
            ? $"✅SMT Sales Auto Create: Success\nTime: {currentTime}"
            : $"❌SMT Sales Auto Create: Failed\nTime: {currentTime}";
        var text = new SendWorkWechatGroupRobotTextDto
        {
            Content = content,
            MentionedMobileList = "@all"
        };

        await _weChatClient.SendWorkWechatRobotMessagesAsync(_salesAutoCreateSetting.NotifyRobotUrl,
            new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = text
            }, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildStoreNamesJson(string storeName)
    {
        return JsonConvert.SerializeObject(new PosNamesLocalization
        {
            En = new PosNamesDetail { Name = storeName },
            Cn = new PosNamesDetail { Name = storeName }
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
