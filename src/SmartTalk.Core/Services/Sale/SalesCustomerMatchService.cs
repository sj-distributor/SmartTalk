using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Crm;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesCustomerMatchService : IScopedDependency
{
    Task<SalesCustomerMatchResult> MatchCustomerAsync(string callerNumber, string calleeNumber, string storeName, IEnumerable<string> salesPhoneNumbers, CancellationToken cancellationToken);
}

public class SalesCustomerMatchResult
{
    public string SoldToId { get; set; } = string.Empty;

    public List<string> SoldToIds { get; set; } = [];

    public string SalesGroup { get; set; } = string.Empty;
}

public class SalesCustomerMatchService : ISalesCustomerMatchService
{
    private readonly ICrmClient _crmClient;
    private readonly ISalesClient _salesClient;

    public SalesCustomerMatchService(ICrmClient crmClient, ISalesClient salesClient)
    {
        _crmClient = crmClient;
        _salesClient = salesClient;
    }

    public async Task<SalesCustomerMatchResult> MatchCustomerAsync(string callerNumber, string calleeNumber, string storeName, IEnumerable<string> salesPhoneNumbers, CancellationToken cancellationToken)
    {
        var crmToken = await TryGetCrmTokenAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(crmToken))
        {
            var phoneMatch = await MatchByPhonesAsync([callerNumber, calleeNumber], crmToken, cancellationToken).ConfigureAwait(false);
            if (phoneMatch.SoldToIds.Count > 0)
                return phoneMatch;

            var storeMatch = await MatchByStoreNameAsync(storeName, crmToken, cancellationToken).ConfigureAwait(false);
            if (storeMatch.SoldToIds.Count > 0)
                return storeMatch;
        }

        var salesGroup = await MatchSalesGroupByPhonesAsync(salesPhoneNumbers, cancellationToken).ConfigureAwait(false);

        return new SalesCustomerMatchResult
        {
            SalesGroup = salesGroup
        };
    }

    private async Task<string> TryGetCrmTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CRM customer matching is skipped because CRM token cannot be obtained.");
            return string.Empty;
        }
    }

    private async Task<SalesCustomerMatchResult> MatchByPhonesAsync(IEnumerable<string> phoneNumbers, string crmToken, CancellationToken cancellationToken)
    {
        var normalizedPhones = phoneNumbers
            .Select(NormalizePhone)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var phoneNumber in normalizedPhones)
        {
            List<GetCustomersPhoneNumberDataDto> customers;

            try
            {
                customers = await _crmClient.GetCustomersByPhoneNumberAsync(
                    new GetCustmoersByPhoneNumberRequestDto { PhoneNumber = phoneNumber },
                    crmToken,
                    cancellationToken).ConfigureAwait(false) ?? [];
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "MatchByPhonesAsync failed for phone {PhoneNumber}", phoneNumber);
                continue;
            }

            var soldToIds = customers
                .Select(x => NormalizeCustomerId(x.SapId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (soldToIds.Count == 0) continue;

            return new SalesCustomerMatchResult
            {
                SoldToId = soldToIds.Count == 1 ? soldToIds[0] : string.Empty,
                SoldToIds = soldToIds
            };
        }

        return new SalesCustomerMatchResult();
    }

    private async Task<SalesCustomerMatchResult> MatchByStoreNameAsync(string storeName, string crmToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeName))
            return new SalesCustomerMatchResult();

        var soldToIds = new List<string>();

        try
        {
            var crmCustomers = await _crmClient.GetCustomersByRestaurantNameAsync(storeName, crmToken, cancellationToken).ConfigureAwait(false);
            soldToIds = crmCustomers
                .Select(x => NormalizeCustomerId(x.SapId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CRM store-name matching failed for store {StoreName}", storeName);
        }

        return new SalesCustomerMatchResult
        {
            SoldToId = soldToIds.Count == 1 ? soldToIds[0] : string.Empty,
            SoldToIds = soldToIds
        };
    }

    private async Task<string> MatchSalesGroupByPhonesAsync(IEnumerable<string> phoneNumbers, CancellationToken cancellationToken)
    {
        var normalizedPhones = phoneNumbers
            .Select(NormalizePhone)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var phoneNumber in normalizedPhones)
        {
            try
            {
                var salesGroup = await _salesClient.GetSalesGroupByPhoneNumberAsync(phoneNumber, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(salesGroup))
                    return salesGroup.Trim();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SalesGroup matching failed for phone {PhoneNumber}", phoneNumber);
            }
        }

        return string.Empty;
    }

    private static string NormalizePhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return string.Empty;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return "+1" + digits;
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal)) return "+" + digits;

        return phoneNumber.Trim();
    }

    private static string NormalizeCustomerId(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return string.Empty;

        var normalized = customerId.Trim().TrimStart('0');
        return string.IsNullOrWhiteSpace(normalized) ? "0" : normalized;
    }
}
