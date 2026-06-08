using System.Text.RegularExpressions;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.Sale;

public static class CrmSalesAutoSyncGrouping
{
    private static readonly Regex AssistantNameRegex = new(@"^(.+?) \((.+)\)$", RegexOptions.Compiled);

    public static string BuildSalesKey(CrmSalesAutoSyncCustomerDto customer)
        => $"{customer.SalesName} {customer.SalesGroup}".Trim();

    public static List<CrmSalesAutoSyncCustomerGroup> BuildCustomerGroups(IEnumerable<CrmSalesAutoSyncCustomerDto> customers)
    {
        return customers
            .GroupBy(BuildSalesKey)
            .SelectMany(group => BuildPhoneMergedGroups(group.Key, group.ToList()))
            .ToList();
    }

    public static Dictionary<string, CrmSalesAutoSyncCustomerGroup> BuildCustomerIdLookup(
        IEnumerable<CrmSalesAutoSyncCustomerGroup> groups)
    {
        var lookup = new Dictionary<string, CrmSalesAutoSyncCustomerGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            foreach (var customerId in group.CustomerIds)
            {
                if (!string.IsNullOrWhiteSpace(customerId))
                    lookup[customerId.Trim()] = group;
            }
        }

        return lookup;
    }

    public static HashSet<string> BuildActiveCustomerIds(IEnumerable<CrmSalesAutoSyncCustomerDto> customers)
        => customers
            .Select(x => x.CustomerId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string BuildAssistantName(IReadOnlyList<string> customerIds, string language)
    {
        var ids = customerIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lang = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim();
        return ids.Count == 0 ? $"({lang})" : $"{string.Join("/", ids)} ({lang})";
    }

    public static bool TryParseAssistantName(string assistantName, out List<string> customerIds, out string language)
    {
        customerIds = new List<string>();
        language = null;

        if (string.IsNullOrWhiteSpace(assistantName))
            return false;

        var match = AssistantNameRegex.Match(assistantName.Trim());
        if (!match.Success)
            return false;

        customerIds = match.Groups[1].Value
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        language = match.Groups[2].Value.Trim();
        return true;
    }

    private static List<CrmSalesAutoSyncCustomerGroup> BuildPhoneMergedGroups(string salesKey, List<CrmSalesAutoSyncCustomerDto> customers)
    {
        var parent = Enumerable.Range(0, customers.Count).ToDictionary(i => i, i => i);
        var contactKeyToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < customers.Count; i++)
        {
            foreach (var contactKey in GetNormalizedContactKeys(customers[i]))
            {
                if (contactKeyToIndex.TryGetValue(contactKey, out var existingIndex))
                    Union(parent, i, existingIndex);
                else
                    contactKeyToIndex[contactKey] = i;
            }
        }

        var components = new Dictionary<int, List<CrmSalesAutoSyncCustomerDto>>();
        var noPhoneCustomers = new List<CrmSalesAutoSyncCustomerDto>();

        for (var i = 0; i < customers.Count; i++)
        {
            if (GetNormalizedContactKeys(customers[i]).Count == 0)
            {
                noPhoneCustomers.Add(customers[i]);
                continue;
            }

            var root = Find(parent, i);
            if (!components.TryGetValue(root, out var list))
            {
                list = new List<CrmSalesAutoSyncCustomerDto>();
                components[root] = list;
            }

            if (!list.Any(x => string.Equals(x.CustomerId, customers[i].CustomerId, StringComparison.OrdinalIgnoreCase)))
                list.Add(customers[i]);
        }

        var result = components.Values.Select(x => ToCustomerGroup(salesKey, x)).ToList();
        result.AddRange(noPhoneCustomers.Select(x => ToCustomerGroup(salesKey, new List<CrmSalesAutoSyncCustomerDto> { x })));
        return result;
    }

    private static CrmSalesAutoSyncCustomerGroup ToCustomerGroup(string salesKey, List<CrmSalesAutoSyncCustomerDto> customers)
    {
        var customerIds = customers
            .Select(x => x.CustomerId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CrmSalesAutoSyncCustomerGroup
        {
            SalesKey = salesKey,
            Customers = customers,
            CustomerIds = customerIds,
            Language = ResolveLanguage(customers)
        };
    }

    private static string ResolveLanguage(IReadOnlyList<CrmSalesAutoSyncCustomerDto> customers)
    {
        foreach (var customer in customers)
        {
            if (!string.IsNullOrWhiteSpace(customer.Language))
                return customer.Language.Trim();
        }

        foreach (var contact in customers.SelectMany(x => x.Contacts ?? new List<ContactDto>()))
        {
            if (!string.IsNullOrWhiteSpace(contact.Language))
                return contact.Language.Trim();
        }

        return "English";
    }

    private static HashSet<string> GetNormalizedContactKeys(CrmSalesAutoSyncCustomerDto customer)
    {
        return (customer.Contacts ?? new List<ContactDto>())
            .Select(BuildNormalizedContactKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildNormalizedContactKey(ContactDto contact)
    {
        var phoneKey = NormalizePhoneKey(contact?.Phone);
        if (string.IsNullOrWhiteSpace(phoneKey))
            return string.Empty;

        var identityKey = NormalizeIdentityKey(contact?.Identity);
        return string.IsNullOrWhiteSpace(identityKey)
            ? phoneKey
            : $"{phoneKey}|{identityKey}";
    }

    private static string NormalizePhoneKey(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        if (digits.Length > 10)
            digits = digits[^10..];

        return digits;
    }

    private static string NormalizeIdentityKey(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return string.Empty;

        return identity.Trim().ToLowerInvariant();
    }

    private static int Find(Dictionary<int, int> parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    private static void Union(Dictionary<int, int> parent, int left, int right)
    {
        var leftRoot = Find(parent, left);
        var rightRoot = Find(parent, right);
        if (leftRoot != rightRoot)
            parent[rightRoot] = leftRoot;
    }
}
