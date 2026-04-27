using Hangfire;
using Hangfire.Throttling;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesJobProcessJobService : IScopedDependency
{
    Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken);
    
    [Semaphore(HangfireConstants.SemaphoreHiFoodCacheCustomerItems)]
    Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken);

    Task ScheduleRefreshCrmCustomerInfoAsync(RefreshAllCustomerInfoCacheCommand command, CancellationToken cancellationToken);

    Task RefreshCrmCustomerInfoByPhoneNumberAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken);
}

public class SalesJobProcessJobService : ISalesJobProcessJobService
{
    private readonly ICrmClient _crmClient;
    private readonly SalesSetting _salesSetting;
    private readonly ISalesService _salesService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public SalesJobProcessJobService(
        ICrmClient crmClient,
        SalesSetting salesSetting,
        ISalesService salesService,
        ISalesDataProvider salesDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        SpeechMaticsDataProvider speechMaticsDataProvide,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _crmClient = crmClient;
        _salesSetting = salesSetting;
        _salesService = salesService;
        _salesDataProvider = salesDataProvider;
        _backgroundJobClient = backgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }
    
    public async Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Start full customer items cache refresh...");

        var allSales = await _salesDataProvider.GetAllSalesAsync(cancellationToken).ConfigureAwait(false);
        var allSoldToIds = allSales.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

        foreach (var soldToId in allSoldToIds)
        {
            _backgroundJobClient.Enqueue<ISalesJobProcessJobService>(
                x => x.RefreshCustomerItemsCacheBySoldToIdAsync(soldToId, CancellationToken.None),
                HangfireConstants.InternalHostingCaCheKnowledgeVariable);
        }

        Log.Information("All customer items cache refresh jobs scheduled. Count: {Count}", allSoldToIds.Count);
    }
    
    public async Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken)
    {
        try
        {
            var ids = soldToId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            Log.Information("Refreshing cache for soldToId: {SoldToId}", ids);

            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                var items = await _salesService.BuildCustomerItemsStringAsync([id], cancellationToken).ConfigureAwait(false);
                var orderArrivalTime = await _salesService.BuildCustomerOrderArrivalTimeStringAsync([id], cancellationToken).ConfigureAwait(false);
                var shouldSave = index == ids.Count - 1;

                await _salesDataProvider.UpsertCustomerItemsCacheAsync(id, items, false, cancellationToken).ConfigureAwait(false);
                await _salesDataProvider.UpsertCustomerOrderArrivalTimeCacheAsync(id, orderArrivalTime, shouldSave, cancellationToken).ConfigureAwait(false);
            }

            Log.Information("Cache refreshed successfully for soldToId: {SoldToId}", soldToId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh cache for soldToId: {SoldToId}", soldToId);
        }
    }
    
    public async Task ScheduleRefreshCrmCustomerInfoAsync(RefreshAllCustomerInfoCacheCommand command, CancellationToken cancellationToken)
    {
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByCompanyIdAsync(_salesSetting.SpecificCompanyId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Customer info cache refresh, Assistants: {@Assistants}.", assistants);
        
        var assistantCustomerMappings = new List<AssistantCustomerMap>();
        
        var defaultAgentNumbers = assistants.Where(x => x.IsDefault).ToDictionary(x => x.AgentId, x => x.AnsweringNumber);
        
        foreach (var assistant in assistants.Where(x => !string.IsNullOrEmpty(x.Name) && x.Name.All(c => char.IsDigit(c) || c == '/')))
        {
            var customerIds = assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            
            var targetNumber = assistant.IsDefault ? assistant.AnsweringNumber : defaultAgentNumbers.TryGetValue(assistant.AgentId, out var number) ? number : null;

            if (string.IsNullOrWhiteSpace(targetNumber)) continue;
            
            assistantCustomerMappings.AddRange(customerIds.Select(x => new AssistantCustomerMap { Id = assistant.Id, TargetNumber = targetNumber, CustomerId = x.Trim() }));
        }
        
        Log.Information("Assistant customer mappings: {@AssistantCustomerMappings}", assistantCustomerMappings);
        
        var crmToken = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(crmToken)) return;

        var totalPhones = 0;
        var scheduledPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var mapping in assistantCustomerMappings)
        {
            var contacts = await _crmClient.GetCustomerContactsAsync(mapping.CustomerId, crmToken, cancellationToken).ConfigureAwait(false);

            var phoneNumbers = contacts?.Where(c => !string.IsNullOrEmpty(c.Phone)).Select(c => NormalizePhone(c.Phone)).Distinct().ToList() ?? new List<string>();

            mapping.CallerNumbers = phoneNumbers;
            totalPhones += phoneNumbers.Count;
            
            foreach (var phone in phoneNumbers)
            {
                if (!scheduledPhones.Add(phone)) continue;
                _backgroundJobClient.Enqueue<ISalesJobProcessJobService>(x => x.RefreshCrmCustomerInfoByPhoneNumberAsync(phone, crmToken, CancellationToken.None), HangfireConstants.InternalHostingCaCheKnowledgeVariable);
            }
        }
        
        Log.Information("Enriched complete assistant customer mappings: {@AssistantCustomerMappings}", assistantCustomerMappings);

        await RefreshCrmCustomerInboundRoutsAsync(assistantCustomerMappings, cancellationToken).ConfigureAwait(false);

        Log.Information(
            "Scheduled CRM customer info refresh for {CustomerCount} customers, {PhoneCount} phone numbers, {UniquePhoneCount} unique phone numbers",
            assistantCustomerMappings.Count,
            totalPhones,
            scheduledPhones.Count);
    }
    
    public async Task RefreshCrmCustomerInfoByPhoneNumberAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Refreshing CRM customer info cache for phone {Phone}", phoneNumber);

            var normalizedPhone = NormalizePhone(phoneNumber);
            var knowledge = await _salesService.BuildCrmKnowledgeByPhoneAsync(normalizedPhone, crmToken, cancellationToken).ConfigureAwait(false);

            await _salesDataProvider.UpsertCustomerInfoCacheAsync(normalizedPhone, knowledge.CustomerInfo, false, cancellationToken).ConfigureAwait(false);
            await _salesDataProvider.UpsertDeliveryInfoCacheAsync(normalizedPhone, knowledge.DeliveryInfo, true, cancellationToken).ConfigureAwait(false);

            Log.Information(
                "CRM customer knowledge cached to ai_speech_assistant_knowledge_variable_cache. CustomerCacheKey: {CustomerCacheKey}, DeliveryCacheKey: {DeliveryCacheKey}, Filter: {Filter}",
                "customer_info",
                "delivery_info",
                normalizedPhone);

            Log.Information("CRM customer info cached successfully for phone {Phone}", phoneNumber);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh CRM customer info cache for phone {Phone}", phoneNumber);
        }
    }

    public async Task RefreshCrmCustomerInboundRoutsAsync(List<AssistantCustomerMap> assistantCustomerMappings, CancellationToken cancellationToken)
    {
        var originalRoutes = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRoutesByTargetNumberAsync(assistantCustomerMappings.Select(x => x.TargetNumber).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantInboundRoutesAsync(originalRoutes, cancellationToken: cancellationToken).ConfigureAwait(false);

        var routes = assistantCustomerMappings.SelectMany(x => x.CallerNumbers.Select(k => new AiSpeechAssistantInboundRoute
        {
            From = k,
            To = x.TargetNumber,
            IsFullDay = true,
            DayOfWeek = string.Empty,
            ForwardAssistantId = x.Id,
            Priority = 0
        })).ToList();

        if (routes.Count != 0)
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantInboundRoutesAsync(routes, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    private string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal)) return "+" + digits;

        return phone.Trim();
    }
}

public class AssistantCustomerMap
{
    public int Id { get; set; }
    
    public string CustomerId { get; set; }
    
    public string TargetNumber { get; set; }
    
    public List<string> CallerNumbers { get; set; }
}
