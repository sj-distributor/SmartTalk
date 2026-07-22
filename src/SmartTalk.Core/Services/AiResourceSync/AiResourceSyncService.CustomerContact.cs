using Serilog;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public partial interface IAiResourceSyncService
{
    Task RefreshCrmCustomerContactPhoneMapsAsync(CancellationToken cancellationToken);
}

public partial class AiResourceSyncService
{
    public async Task RefreshCrmCustomerContactPhoneMapsAsync(CancellationToken cancellationToken)
    {
        var company = await _posDataProvider.GetPosCompanyByNameAsync(_salesSetting.CompanyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
            throw new Exception($"Sales company [{_salesSetting.CompanyName}] not found.");

        var (allCustomers, _) = await _crmClient.GetSalesAutoSyncCustomersAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        allCustomers ??= [];

        var customerGroups = CrmSalesAutoSyncGrouping.BuildCustomerGroups(allCustomers);
        var existingStores = await _posDataProvider
            .GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var storeMap = existingStores
            .Select(x => new { Store = x, StoreName = GetStoreName(x.Names) })
            .Where(x => !string.IsNullOrWhiteSpace(x.StoreName))
            .GroupBy(x => x.StoreName)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Store.CreatedDate).First().Store, StringComparer.OrdinalIgnoreCase);

        await SyncCustomerContactPhoneMapsAsync(company.Id, customerGroups, storeMap, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task SyncCustomerContactPhoneMapsAsync(int companyId, IReadOnlyList<CrmSalesAutoSyncCustomerGroup> customerGroups, 
        IReadOnlyDictionary<string, CompanyStore> storeMap, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var desiredMappings = await BuildDesiredCustomerContactPhoneMapsAsync(
            companyId, customerGroups, storeMap, now, cancellationToken).ConfigureAwait(false);

        var desiredByKey = desiredMappings
            .GroupBy(BuildCustomerContactPhoneMapKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var existingMappings = await _aiSpeechAssistantDataProvider
            .GetCrmCustomerContactPhoneMapsByCompanyIdAsync(companyId, cancellationToken)
            .ConfigureAwait(false) ?? [];

        var (toAdd, toUpdate) = BuildCustomerContactPhoneMapChanges(desiredByKey, existingMappings, now);

        if (toAdd.Count > 0)
            await _aiSpeechAssistantDataProvider.AddCrmCustomerContactPhoneMapsAsync(toAdd, true, cancellationToken).ConfigureAwait(false);

        if (toUpdate.Count > 0)
            await _aiSpeechAssistantDataProvider.UpdateCrmCustomerContactPhoneMapsAsync(toUpdate.DistinctBy(x => x.Id).ToList(), true, cancellationToken).ConfigureAwait(false);

        Log.Information(
            "CRM contact phone map synced. CompanyId={CompanyId}, DesiredCount={DesiredCount}, Added={AddedCount}, Updated={UpdatedCount}",
            companyId, desiredByKey.Count, toAdd.Count, toUpdate.Select(x => x.Id).Distinct().Count());
    }

    private async Task<List<CrmCustomerContactPhoneMap>> BuildDesiredCustomerContactPhoneMapsAsync(
        int companyId, IReadOnlyList<CrmSalesAutoSyncCustomerGroup> customerGroups,
        IReadOnlyDictionary<string, CompanyStore> storeMap, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var desiredMappings = new List<CrmCustomerContactPhoneMap>();

        foreach (var customerGroup in customerGroups)
        {
            if (!storeMap.TryGetValue(customerGroup.SalesKey, out var store))
                continue;

            var assistantName = CrmSalesAutoSyncGrouping.BuildAssistantName(customerGroup.CustomerIds, customerGroup.Language);
            var assistant = await _aiSpeechAssistantDataProvider
                .GetCrmAutoSyncAssistantByStoreAndNameAsync(store.Id, assistantName, cancellationToken)
                .ConfigureAwait(false);

            if (assistant == null)
                continue;

            desiredMappings.AddRange(BuildCustomerContactPhoneMaps(companyId, customerGroup, assistant, now));
        }

        return desiredMappings;
    }

    private static IEnumerable<CrmCustomerContactPhoneMap> BuildCustomerContactPhoneMaps(
        int companyId, CrmSalesAutoSyncCustomerGroup customerGroup, SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant assistant, DateTimeOffset now)
    {
        return from customer in customerGroup.Customers
            from contact in customer.Contacts ?? []
            let normalizedPhone = CrmSalesAutoSyncGrouping.NormalizePhoneNumber(contact?.Phone)
            where !string.IsNullOrWhiteSpace(normalizedPhone) && !string.IsNullOrWhiteSpace(customer.CustomerId)
            select new CrmCustomerContactPhoneMap
            {
                CompanyId = companyId,
                AgentId = assistant.AgentId,
                AssistantId = assistant.Id,
                CustomerId = customer.CustomerId.Trim(),
                CustomerName = customer.CustomerName?.Trim(),
                ContactName = contact?.Name?.Trim(),
                ContactIdentity = contact?.Identity?.Trim(),
                ContactLanguage = contact?.Language?.Trim(),
                ContactPhoneRaw = contact?.Phone?.Trim(),
                ContactPhoneNormalized = normalizedPhone,
                IsActive = true,
                CreatedDate = now,
                LastModifiedDate = now
            };
    }

    private static (List<CrmCustomerContactPhoneMap> ToAdd, List<CrmCustomerContactPhoneMap> ToUpdate) BuildCustomerContactPhoneMapChanges(
        IReadOnlyDictionary<string, CrmCustomerContactPhoneMap> desiredByKey,
        IReadOnlyList<CrmCustomerContactPhoneMap> existingMappings,
        DateTimeOffset now)
    {
        var existingByKey = existingMappings.ToDictionary(BuildCustomerContactPhoneMapKey, StringComparer.OrdinalIgnoreCase);
        var toAdd = new List<CrmCustomerContactPhoneMap>();
        var toUpdate = new List<CrmCustomerContactPhoneMap>();

        foreach (var (key, desired) in desiredByKey)
        {
            if (!existingByKey.TryGetValue(key, out var existing))
            {
                toAdd.Add(desired);
                continue;
            }

            existing.CustomerName = desired.CustomerName;
            existing.ContactName = desired.ContactName;
            existing.ContactIdentity = desired.ContactIdentity;
            existing.ContactLanguage = desired.ContactLanguage;
            existing.ContactPhoneRaw = desired.ContactPhoneRaw;
            existing.IsActive = true;
            existing.LastModifiedDate = now;
            toUpdate.Add(existing);
        }

        foreach (var (key, existing) in existingByKey)
        {
            if (desiredByKey.ContainsKey(key) || !existing.IsActive)
                continue;

            existing.IsActive = false;
            existing.LastModifiedDate = now;
            toUpdate.Add(existing);
        }

        return (toAdd, toUpdate);
    }

    private static string BuildCustomerContactPhoneMapKey(CrmCustomerContactPhoneMap mapping)
    {
        return $"{mapping.CustomerId}|{mapping.AgentId}|{mapping.AssistantId}|{mapping.ContactPhoneNormalized}";
    }
}
