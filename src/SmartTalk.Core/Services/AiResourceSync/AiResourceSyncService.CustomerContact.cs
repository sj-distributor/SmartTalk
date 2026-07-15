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
    private async Task SyncCustomerContactPhoneMapsAsync(
        int companyId,
        IReadOnlyList<CrmSalesAutoSyncCustomerGroup> customerGroups,
        IReadOnlyDictionary<string, CompanyStore> storeMap,
        CancellationToken cancellationToken)
    {
        var desiredMappings = new List<CrmCustomerContactPhoneMap>();
        var now = DateTimeOffset.UtcNow;

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

            foreach (var customer in customerGroup.Customers)
            {
                foreach (var contact in customer.Contacts ?? [])
                {
                    var normalizedPhone = CrmSalesAutoSyncGrouping.NormalizePhoneNumber(contact?.Phone);
                    if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(customer.CustomerId))
                        continue;

                    desiredMappings.Add(new CrmCustomerContactPhoneMap
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
                    });
                }
            }
        }

        var desiredByKey = desiredMappings
            .GroupBy(x => $"{x.CustomerId}|{x.AgentId}|{x.AssistantId}|{x.ContactPhoneNormalized}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var existingMappings = await _aiSpeechAssistantDataProvider
            .GetCrmCustomerContactPhoneMapsByCompanyIdAsync(companyId, cancellationToken)
            .ConfigureAwait(false) ?? [];

        var existingByKey = existingMappings.ToDictionary(
            x => $"{x.CustomerId}|{x.AgentId}|{x.AssistantId}|{x.ContactPhoneNormalized}",
            StringComparer.OrdinalIgnoreCase);

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

        if (toAdd.Count > 0)
            await _aiSpeechAssistantDataProvider.AddCrmCustomerContactPhoneMapsAsync(toAdd, true, cancellationToken).ConfigureAwait(false);

        if (toUpdate.Count > 0)
            await _aiSpeechAssistantDataProvider.UpdateCrmCustomerContactPhoneMapsAsync(toUpdate.DistinctBy(x => x.Id).ToList(), true, cancellationToken).ConfigureAwait(false);

        Log.Information(
            "CRM contact phone map synced. CompanyId={CompanyId}, DesiredCount={DesiredCount}, Added={AddedCount}, Updated={UpdatedCount}",
            companyId, desiredByKey.Count, toAdd.Count, toUpdate.Select(x => x.Id).Distinct().Count());
    }

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
}
