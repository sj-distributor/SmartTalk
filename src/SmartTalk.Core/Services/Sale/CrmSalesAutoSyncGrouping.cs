using System.Text.RegularExpressions;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.Sale;

public static class CrmSalesAutoSyncGrouping
{
    private static readonly Regex AssistantNameRegex = new(@"^(.+?) \((.+)\)$", RegexOptions.Compiled);

    public static string BuildSalesKey(CrmSalesAutoSyncCustomerDto customer)
        => string.Join(" ", new[] { customer.SalesName, customer.SalesGroup }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public static List<CrmSalesAutoSyncCustomerGroup> BuildCustomerGroups(IEnumerable<CrmSalesAutoSyncCustomerDto> customers)
    {
        return customers
            .GroupBy(BuildSalesKey)
            .SelectMany(group => BuildPhoneMergedGroups(group.Key, group.ToList()))
            .ToList();
    }

    public static Dictionary<string, CrmSalesAutoSyncCustomerGroup> BuildCustomerIdLookup(IEnumerable<CrmSalesAutoSyncCustomerGroup> groups)
        => groups
            .SelectMany(group => group.CustomerIds.Select(customerId => new { customerId, group }))
            .Where(x => !string.IsNullOrWhiteSpace(x.customerId))
            .ToDictionary(x => x.customerId, x => x.group, StringComparer.OrdinalIgnoreCase);

    public static HashSet<string> BuildActiveCustomerIds(IEnumerable<CrmSalesAutoSyncCustomerDto> customers)
        => customers
            .Select(x => x.CustomerId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string BuildAssistantName(IReadOnlyList<string> customerIds, string language)
    {
        var ids = customerIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join("/", ids);
    }

    public static bool TryParseAssistantName(string assistantName, out List<string> customerIds, out string language)
    {
        customerIds = new List<string>();
        language = null;

        if (string.IsNullOrWhiteSpace(assistantName))
            return false;

        var normalizedName = assistantName.Trim();
        var match = AssistantNameRegex.Match(normalizedName);
        var rawIds = match.Success ? match.Groups[1].Value : normalizedName;
        if (match.Success)
            language = match.Groups[2].Value.Trim();

        customerIds = rawIds
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return customerIds.Count > 0;
    }

    private static List<CrmSalesAutoSyncCustomerGroup> BuildPhoneMergedGroups(string salesKey, List<CrmSalesAutoSyncCustomerDto> customers)
    {
        var customerContactKeys = customers.Select(GetNormalizedContactKeys).ToList();
        var parent = Enumerable.Range(0, customers.Count).ToArray();
        var contactKeyToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < customers.Count; i++)
        {
            foreach (var contactKey in customerContactKeys[i])
            {
                if (contactKeyToIndex.TryGetValue(contactKey, out var existingIndex))
                    Union(parent, i, existingIndex);
                else
                    contactKeyToIndex[contactKey] = i;
            }
        }

        var componentCustomers = new Dictionary<int, List<CrmSalesAutoSyncCustomerDto>>();
        var customersWithoutPhones = new List<CrmSalesAutoSyncCustomerDto>();

        for (var i = 0; i < customers.Count; i++)
        {
            if (customerContactKeys[i].Count == 0)
            {
                customersWithoutPhones.Add(customers[i]);
                continue;
            }

            var root = Find(parent, i);
            if (!componentCustomers.TryGetValue(root, out var groupedCustomers))
                componentCustomers[root] = groupedCustomers = new List<CrmSalesAutoSyncCustomerDto>();

            if (groupedCustomers.Any(x => string.Equals(x.CustomerId, customers[i].CustomerId, StringComparison.OrdinalIgnoreCase)))
                continue;

            groupedCustomers.Add(customers[i]);
        }

        return componentCustomers.Values
            .Select(groupedCustomers => ToCustomerGroup(salesKey, groupedCustomers))
            .Concat(customersWithoutPhones.Select(customer => ToCustomerGroup(salesKey, new List<CrmSalesAutoSyncCustomerDto> { customer })))
            .ToList();
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
                return customer.Language;
        }
        
        return "英文";
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
        return NormalizePhoneKey(contact?.Phone);
    }

    public static string NormalizePhoneNumber(string phoneNumber)
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

    private static string NormalizePhoneKey(string phoneNumber)
    {
        return NormalizePhoneNumber(phoneNumber);
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    private static void Union(int[] parent, int left, int right)
    {
        var leftRoot = Find(parent, left);
        var rightRoot = Find(parent, right);
        if (leftRoot != rightRoot)
            parent[rightRoot] = leftRoot;
    }
}
