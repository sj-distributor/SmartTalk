using Hangfire;
using Hangfire.Throttling;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Settings.Jobs;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesJobProcessJobService : IScopedDependency
{
    Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken);

    [Semaphore(HangfireConstants.SemaphoreHiFoodCacheCustomerItems)]
    Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken);

    [Semaphore(HangfireConstants.SemaphoreHiFoodCacheCustomerItems)]
    Task RefreshCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken);

    Task ScheduleRefreshCrmCustomerInfoAsync(RefreshAllCustomerInfoCacheCommand command, CancellationToken cancellationToken);

    Task RefreshCrmCustomerInfoByPhoneNumberAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken);
}

public class SalesJobProcessJobService : ISalesJobProcessJobService
{
    private readonly ICrmClient _crmClient;
    private readonly ISalesService _salesService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly CustomerItemsRefreshBatchSizeSetting _customerItemsRefreshBatchSizeSetting;

    public SalesJobProcessJobService(
        ICrmClient crmClient,
        ISalesService salesService,
        ISalesDataProvider salesDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        CustomerItemsRefreshBatchSizeSetting customerItemsRefreshBatchSizeSetting)
    {
        _crmClient = crmClient;
        _salesService = salesService;
        _salesDataProvider = salesDataProvider;
        _backgroundJobClient = backgroundJobClient;
        _customerItemsRefreshBatchSizeSetting = customerItemsRefreshBatchSizeSetting;
    }

    public async Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Start full customer items cache refresh...");

        var allSales = await _salesDataProvider.GetAllSalesAsync(cancellationToken).ConfigureAwait(false);
        var allSoldToIds = NormalizeSoldToIds(allSales.Select(s => s.Name));
        var batchSize = _customerItemsRefreshBatchSizeSetting.Value;

        foreach (var soldToIdBatch in allSoldToIds.Chunk(batchSize))
        {
            var batch = soldToIdBatch.ToList();
            _backgroundJobClient.Enqueue<ISalesJobProcessJobService>(
                x => x.RefreshCustomerItemsCacheBySoldToIdsAsync(batch, CancellationToken.None),
                HangfireConstants.InternalHostingCaCheKnowledgeVariable);
        }

        Log.Information(
            "All customer items cache refresh jobs scheduled. CustomerCount: {CustomerCount}, JobCount: {JobCount}, BatchSize: {BatchSize}",
            allSoldToIds.Count,
            (int)Math.Ceiling(allSoldToIds.Count / (double)batchSize),
            batchSize);
    }

    public async Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken)
    {
        await RefreshCustomerItemsCacheBySoldToIdsAsync([soldToId], cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        try
        {
            var ids = NormalizeSoldToIds(soldToIds);
            if (ids.Count == 0)
            {
                Log.Warning("RefreshCustomerItemsCacheBySoldToIdsAsync called with empty soldToIds");
                return;
            }

            Log.Information("Refreshing customer items cache for soldToIds: {SoldToIds}", ids);

            var customerItems = await _salesService.BuildCustomerItemsStringsAsync(ids, cancellationToken).ConfigureAwait(false);
            var cacheItems = ids.ToDictionary(
                x => x,
                x => customerItems.GetValueOrDefault(x) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
            await _salesDataProvider.UpsertCustomerItemsCachesAsync(cacheItems, cancellationToken).ConfigureAwait(false);

            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                var deliveryProgress = await _salesService.BuildCustomerDeliveryProgressStringAsync([id], cancellationToken).ConfigureAwait(false);
                var shouldSave = index == ids.Count - 1;

                await _salesDataProvider.UpsertDeliveryProgressCacheAsync(id, deliveryProgress, shouldSave, cancellationToken).ConfigureAwait(false);
            }

            Log.Information("Customer items cache refreshed successfully for soldToIds: {SoldToIds}", ids);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh customer items cache for soldToIds: {SoldToIds}", soldToIds);
        }
    }

    private static List<string> NormalizeSoldToIds(IEnumerable<string> soldToIds)
    {
        return (soldToIds ?? Enumerable.Empty<string>())
            .SelectMany(x => (x ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    public async Task ScheduleRefreshCrmCustomerInfoAsync(RefreshAllCustomerInfoCacheCommand command, CancellationToken cancellationToken)
    {
        var allSales = await _salesDataProvider.GetAllSalesAsync(cancellationToken).ConfigureAwait(false);
        var allSoldToIds = allSales.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();

        var crmToken = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(crmToken)) return;

        var totalPhones = 0;
        var phoneNumbersToRefresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var soldToId in allSoldToIds)
        {
            var customerIds = soldToId
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            foreach (var customerId in customerIds)
            {
                var contacts = await _crmClient.GetCustomerContactsAsync(customerId, crmToken, cancellationToken).ConfigureAwait(false);
                if (contacts == null || contacts.Count == 0) continue;

                var phoneNumbers = contacts
                    .Where(c => !string.IsNullOrEmpty(c.Phone))
                    .Select(c => NormalizePhone(c.Phone))
                    .Where(phone => !string.IsNullOrEmpty(phone))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                totalPhones += phoneNumbers.Count;

                foreach (var phone in phoneNumbers)
                {
                    phoneNumbersToRefresh.Add(phone);
                }
            }
        }

        foreach (var phone in phoneNumbersToRefresh)
        {
            _backgroundJobClient.Enqueue<ISalesJobProcessJobService>(
                x => x.RefreshCrmCustomerInfoByPhoneNumberAsync(phone, crmToken, CancellationToken.None),
                HangfireConstants.InternalHostingCaCheKnowledgeVariable);
        }

        Log.Information(
            "Scheduled CRM customer info refresh for {SoldToIdCount} sold-to entries, {PhoneCount} phone numbers, {UniquePhoneCount} unique phone numbers",
            allSoldToIds.Count,
            totalPhones,
            phoneNumbersToRefresh.Count);
    }

    public async Task RefreshCrmCustomerInfoByPhoneNumberAsync(string phoneNumber, string crmToken, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Refreshing CRM customer info cache for phone {Phone}", phoneNumber);

            var normalizedPhone = NormalizePhone(phoneNumber);
            var knowledge = await _salesService.BuildCrmKnowledgeByPhoneAsync(normalizedPhone, crmToken, cancellationToken).ConfigureAwait(false);

            await _salesDataProvider.UpsertCustomerInfoCacheAsync(normalizedPhone, knowledge.CustomerInfo, false, cancellationToken)
                .ConfigureAwait(false);
            await _salesDataProvider.UpsertDeliveryInfoCacheAsync(normalizedPhone, knowledge.DeliveryInfo, true, cancellationToken)
                .ConfigureAwait(false);

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

    private string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal)) return "+" + digits;

        return phone.Trim();
    }
}
