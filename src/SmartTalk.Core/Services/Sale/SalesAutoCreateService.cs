using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Core.Utils;
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

namespace SmartTalk.Core.Services.Sale;

public interface ISalesAutoCreateService
{
    Task<SyncCrmSalesAutoCreateResponse> SyncCrmSalesAutoCreateAsync(SyncCrmSalesAutoCreateCommand command, CancellationToken cancellationToken);
}

public class SalesAutoCreateService : ISalesAutoCreateService
{
    private readonly IMediator _mediator;
    private readonly ICrmClient _crmClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IKnowledgeScenarioDataProvider _knowledgeScenarioDataProvider;
    private readonly IWeChatClient _weChatClient;
    private readonly SalesSetting _salesSetting;
    private readonly SalesAutoCreateSetting _salesAutoCreateSetting;

    public SalesAutoCreateService(
        IMediator mediator,
        ICrmClient crmClient,
        IPosDataProvider posDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IKnowledgeScenarioDataProvider knowledgeScenarioDataProvider,
        IWeChatClient weChatClient,
        SalesSetting salesSetting,
        SalesAutoCreateSetting salesAutoCreateSetting)
    {
        _mediator = mediator;
        _crmClient = crmClient;
        _posDataProvider = posDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
        _weChatClient = weChatClient;
        _salesSetting = salesSetting;
        _salesAutoCreateSetting = salesAutoCreateSetting;
    }

    public async Task<SyncCrmSalesAutoCreateResponse> SyncCrmSalesAutoCreateAsync(SyncCrmSalesAutoCreateCommand command, CancellationToken cancellationToken)
    {
        SyncCrmSalesAutoCreateResponse response = null;

        try
        {
            response = await RetryHelper.RetryAsync(
                () => SyncInternalAsync(command, cancellationToken), maxRetryCount: 2, delaySeconds: 300, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!command.IsManual)
                await SendNotifyAsync(true, response.Data, null, cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SyncCrmSalesAutoCreate failed");

            await SendNotifyAsync(false, response?.Data, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<SyncCrmSalesAutoCreateResponse> SyncInternalAsync(SyncCrmSalesAutoCreateCommand command, CancellationToken cancellationToken)
    {
        var query = BuildQuery(command);
        var customers = await _crmClient.GetSalesAutoSyncCustomersAsync(query, cancellationToken).ConfigureAwait(false);
        var result = new SyncCrmSalesAutoCreateResponseData
        {
            TotalCount = customers.Count
        };

        var companyName = _salesSetting.CompanyName?.Trim();
        if (string.IsNullOrWhiteSpace(companyName))
            throw new Exception("Sales CompanyName is not configured.");

        var company = await _posDataProvider.GetPosCompanyByNameAsync(companyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
            throw new Exception($"Sales company [{companyName}] not found.");

        var customerGroups = customers.GroupBy(x => $"{x.SalesName} {x.SalesGroup}").ToList();

        var storeNames = customerGroups.Select(x => x.Key).Distinct().ToList();
        
        var existingStores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var storeMap = existingStores
            .Where(x => storeNames.Contains(x.Names))
            .GroupBy(x => x.Names)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.CreatedDate).First());

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
                await EnsureCustomerKnowledgeAsync(command, store.Id, customer, result, cancellationToken).ConfigureAwait(false);
        }

        return new SyncCrmSalesAutoCreateResponse
        {
            Data = result
        };
    }

    private CrmSalesAutoSyncQueryDto BuildQuery(SyncCrmSalesAutoCreateCommand command)
    {
        var pst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var nowPst = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, pst);

        if (command.StartAt.HasValue || command.EndAt.HasValue)
        {
            return new CrmSalesAutoSyncQueryDto
            {
                StartAt = command.StartAt,
                EndAt = command.EndAt,
                IsInitialSync = command.IsInitialSync,
                IsManual = command.IsManual
            };
        }

        if (command.IsManual)
        {
            return new CrmSalesAutoSyncQueryDto
            {
                StartAt = new DateTimeOffset(nowPst.Year, nowPst.Month, nowPst.Day, 0, 0, 0, nowPst.Offset),
                EndAt = nowPst,
                IsInitialSync = command.IsInitialSync,
                IsManual = true
            };
        }

        var yesterdayStart = new DateTimeOffset(nowPst.Year, nowPst.Month, nowPst.Day, 0, 0, 0, nowPst.Offset).AddDays(-1);

        return new CrmSalesAutoSyncQueryDto
        {
            StartAt = yesterdayStart,
            EndAt = yesterdayStart.AddDays(1),
            IsInitialSync = command.IsInitialSync,
            IsManual = false
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
            Names = storeName,
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
            Greetings = _salesAutoCreateSetting.DefaultAssistantGreetings ?? "Hello, how can I help you today?",
            AgentType = AgentType.PosCompanyStore,
            SourceSystem = AgentSourceSystem.CrmAutoSync,
            ModelLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim(),
            Channels = new List<AiSpeechAssistantChannel> { AiSpeechAssistantChannel.PhoneChat },
            Details = new List<AiSpeechAssistantKnowledgeDetailDto>()
        }, cancellationToken).ConfigureAwait(false);

        result.CreatedAssistantCount++;
        return await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(created.Data.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Core.Domain.AISpeechAssistant.AiSpeechAssistant> EnsureCustomerKnowledgeAsync(
        SyncCrmSalesAutoCreateCommand command, int storeId, CrmSalesAutoSyncCustomerDto customer,
        SyncCrmSalesAutoCreateResponseData result, CancellationToken cancellationToken)
    {
        if (!customer.IsApproved)
        {
            result.Warnings.Add($"Customer [{customer.CustomerId}] is not approved yet. Skipped customer knowledge.");
            return null;
        }

        var customerAssistantName =
            $"{customer.CustomerId} ({(string.IsNullOrWhiteSpace(customer.Language) ? "English" : customer.Language.Trim())})";
        var customerAssistant = await EnsureSalesAssistantAsync(
            command,
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

        var sourceSceneIds = await ResolveSourceSceneIdsAsync(storeId, customer, cancellationToken).ConfigureAwait(false);
        if (sourceSceneIds.Count == 0)
        {
            result.Warnings.Add(
                $"Customer [{customer.CustomerId}] language [{customer.Language}] has no source scene mapping yet. " +
                "Current implementation assumes CRM pull data; if CRM later provides approval callback, callback can still trigger this same sync method.");
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

    private async Task<List<int>> ResolveSourceSceneIdsAsync(int storeId, CrmSalesAutoSyncCustomerDto customer, CancellationToken cancellationToken)
    {
        if (customer.SourceSceneIds is { Count: > 0 })
            return customer.SourceSceneIds.Distinct().ToList();

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

        var scenes = await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
        var language = string.IsNullOrWhiteSpace(customer.Language) ? "English" : customer.Language.Trim();

        return scenes
            .Where(x => x.Status == KnowledgeSceneStatus.Published
                && x.Name.Contains(language, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToList();
    }

    private async Task SendNotifyAsync(
        bool isSuccess,
        SyncCrmSalesAutoCreateResponseData data,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_salesAutoCreateSetting.NotifyRobotUrl))
            return;

        var content = isSuccess
            ? $"CRM Sales Auto Create Success\n{JsonConvert.SerializeObject(data)}"
            : $"CRM Sales Auto Create Failed\n{errorMessage}";

        await _weChatClient.SendWorkWechatRobotMessagesAsync(
            _salesAutoCreateSetting.NotifyRobotUrl,
            new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = new SendWorkWechatGroupRobotTextDto
                {
                    Content = content
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

}
