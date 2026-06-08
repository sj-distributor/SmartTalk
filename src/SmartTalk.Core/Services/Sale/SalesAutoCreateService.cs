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
        var result = new SyncCrmSalesAutoCreateResponseData { TotalCount = customers.Count };

        var company = await _posDataProvider.GetPosCompanyByNameAsync(_salesSetting.CompanyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
            throw new Exception($"Sales company [{_salesSetting.CompanyName}] not found.");

        result.IsInitialRelease = !command.IsManual
            && !await _aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(company.Id, cancellationToken).ConfigureAwait(false);

        // 同一个跟进 Sales 共用一个 store 和 sales assistant，所以先按 Sales 维度分组。
        var customerGroups = customers.GroupBy(x => $"{x.SalesName} {x.SalesGroup}").ToList();

        var storeNames = customerGroups.Select(x => x.Key).Distinct().ToList();
        
        var existingStores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);

        // CompanyStore.Names 存的是多语言 JSON，这里统一提取展示名做匹配。
        var storeMap = existingStores
            .Select(x => new { Store = x, StoreName = GetStoreName(x.Names) })
            .Where(x => !string.IsNullOrWhiteSpace(x.StoreName) && storeNames.Contains(x.StoreName))
            .GroupBy(x => x.StoreName)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Store.CreatedDate).First().Store);

        foreach (var customerGroup in customerGroups)
        {
            var seedCustomer = customerGroup.First();
            var storeName = customerGroup.Key;
            var primaryLanguage = customerGroup
                .Select(x => string.IsNullOrWhiteSpace(x.Language) ? "English" : x.Language.Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            var store = await EnsureSalesStoreAsync(seedCustomer, company.Id, storeMap, result, cancellationToken).ConfigureAwait(false);
            var assistant = await EnsureSalesAssistantAsync(command, store.Id, storeName, primaryLanguage, result, cancellationToken).ConfigureAwait(false);

            foreach (var customer in customerGroup)
                await EnsureCustomerKnowledgeAsync(command, company.Id, store.Id, customer, result, cancellationToken).ConfigureAwait(false);
        }

        return new SyncCrmSalesAutoCreateResponse
        {
            Data = result
        };
    }

    private async Task<Core.Domain.Pos.CompanyStore> EnsureSalesStoreAsync(
        CrmSalesAutoSyncCustomerDto customer, int companyId, Dictionary<string, Core.Domain.Pos.CompanyStore> storeMap,
        SyncCrmSalesAutoCreateResponseData result, CancellationToken cancellationToken)
    {
        var storeName = $"{customer.SalesName} {customer.SalesGroup}";
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

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> EnsureCustomerKnowledgeAsync(
        SyncCrmSalesAutoCreateCommand command, int companyId, int storeId, CrmSalesAutoSyncCustomerDto customer,
        SyncCrmSalesAutoCreateResponseData result, CancellationToken cancellationToken)
    {
        var customerAssistantName = BuildCustomerAssistantName(customer);
        var customerAssistant = await ResolveCustomerAssistantAsync(
            command,
            companyId,
            storeId,
            customerAssistantName,
            customer.Language,
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
                Greetings = _salesAutoCreateSetting.DefaultAssistantGreetings ?? "Hello, how can I help you today?",
                Json = "{}",
                Language = string.IsNullOrWhiteSpace(customer.Language) ? "English" : customer.Language.Trim(),
                Premise = $"CRM Customer Knowledge {customer.CustomerId}",
                Details = new List<AiSpeechAssistantKnowledgeDetailDto>()
            }, cancellationToken).ConfigureAwait(false);

            result.CreatedKnowledgeCount++;

            activeKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
                assistantId: customerAssistant.Id,
                isActive: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // 按公司下已发布场景和语言匹配源场景。
        var sourceSceneIds = await ResolveSourceSceneIdsAsync(storeId, customer, cancellationToken).ConfigureAwait(false);
        if (sourceSceneIds.Count == 0)
        {
            result.Warnings.Add(
                $"Customer [{customer.CustomerId}] language [{customer.Language}] has no source scene mapping yet.");
            return customerAssistant;
        }

        foreach (var sceneId in sourceSceneIds)
        {
            var scene = (await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync([sceneId], cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            if (scene == null || scene.Status != KnowledgeSceneStatus.Published)
            {
                result.Warnings.Add($"Scene [{sceneId}] is unavailable for customer [{customer.CustomerId}].");
                continue;
            }

            var store = await _posDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);

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
                AssistantIds = new List<int> { customerAssistant.Id }
            }, cancellationToken).ConfigureAwait(false);

            result.AppliedSceneCount++;
        }

        return customerAssistant;
    }

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> ResolveCustomerAssistantAsync(
        SyncCrmSalesAutoCreateCommand command, int companyId, int targetStoreId, string assistantName, string language,
        SyncCrmSalesAutoCreateResponseData result, CancellationToken cancellationToken)
    {
        var existing = await _aiSpeechAssistantDataProvider
            .GetCrmAutoSyncAssistantByNameInCompanyAsync(companyId, assistantName, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            if (existing.StoreId != targetStoreId)
            {
                await TransferCustomerAssistantToStoreAsync(existing, targetStoreId, cancellationToken).ConfigureAwait(false);
                result.TransferredAssistantCount++;
            }

            return await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantByIdAsync(existing.AssistantId, cancellationToken)
                .ConfigureAwait(false);
        }

        return await EnsureSalesAssistantAsync(command, targetStoreId, assistantName, language, result, cancellationToken)
            .ConfigureAwait(false);
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

    private static string BuildCustomerAssistantName(CrmSalesAutoSyncCustomerDto customer)
        => $"{customer.CustomerId} ({(string.IsNullOrWhiteSpace(customer.Language) ? "English" : customer.Language.Trim())})";

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

        var language = string.IsNullOrWhiteSpace(customer.Language) ? "English" : customer.Language.Trim();
        var mappings = await _knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(
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
        // 自动创建的店铺名称也要保持和现有 POS 多语言名称结构一致。
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
