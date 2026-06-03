using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.SjFood;

namespace SmartTalk.Core.Services.SjFood;

public interface ISjFoodQuotationService : IScopedDependency
{
    Task<SjFoodQuotationResult> QueryPriceByPhoneAndProductAsync(string phoneNumber, string productName, SjFoodCustomerMatchHints customerHints, CancellationToken cancellationToken);
}

public class SjFoodQuotationService : ISjFoodQuotationService
{
    private const string MissingProductMessage = "Missing product_name. Ask the user which product they want the price for.";
    private const string MissingCustomerMessage = "Unable to identify exactly one CRM customer for this phone number, so do not quote a price yet. Ask the customer for one more identifier: restaurant/customer name, street or brand street, header note/remark, contact name, contact identity, or SAP customer ID, then call get_product_price again with that detail.";
    private const string MultipleCustomersMessage = "This phone number is linked to multiple CRM customers, so do not quote a price yet. Ask which restaurant/customer they mean, preferably restaurant name, street or brand street, header note/remark, contact name, contact identity, or SAP customer ID, then call get_product_price again with that detail.";
    private const string NoMatchingPriceMessage = "No matching price available";

    private readonly ICrmClient _crmClient;
    private readonly ISjFoodClient _sjFoodClient;

    public SjFoodQuotationService(ICrmClient crmClient, ISjFoodClient sjFoodClient)
    {
        _crmClient = crmClient;
        _sjFoodClient = sjFoodClient;
    }

    public async Task<SjFoodQuotationResult> QueryPriceByPhoneAndProductAsync(string phoneNumber, string productName, SjFoodCustomerMatchHints customerHints, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return SjFoodQuotationResult.WithMessage(MissingProductMessage);

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return SjFoodQuotationResult.WithMessage(MissingCustomerMessage);

        var normalizedPhone = NormalizePhone(phoneNumber);
        
        try
        {
            var token = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
            var customers = await _crmClient
                .GetCustomersByPhoneNumberAsync(new GetCustmoersByPhoneNumberRequestDto { PhoneNumber = normalizedPhone }, token, cancellationToken)
                .ConfigureAwait(false);

            var customer = ResolveCustomer(customers, customerHints);
            if (customer == null)
                return SjFoodQuotationResult.WithMessage(BuildCustomerDisambiguationMessage(customers, customerHints));

            var response = await _sjFoodClient.GetCustomerAiQuotationAsync(new GetCustomerAiQuotationRequestDto
            {
                CustomerId = customer.SapId,
                ProductNameList = [productName]
            }, cancellationToken).ConfigureAwait(false);

            if (response?.AiQuotationList == null || response.AiQuotationList.Count == 0)
            {
                Log.Information("No quotation found for phone {PhoneNumber}, customer {CustomerId}, hints {@CustomerHints}, product {ProductName}. Message: {Message}",
                    normalizedPhone, customer.SapId, customerHints, productName, response?.Message);
                return SjFoodQuotationResult.WithMessage($"{productName}：{NoMatchingPriceMessage}", customer.SapId);
            }

            var priceText = string.Join("；", response.AiQuotationList
                .Select(FormatQuotation)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            Log.Information(
                "Function:{Function} result. PhoneNumber: {PhoneNumber}, CustomerSapId: {CustomerSapId}, ProductName: {ProductName}, Price: {Price}",
                "get_product_price", normalizedPhone, customer.SapId, productName, priceText);
            
            return string.IsNullOrWhiteSpace(priceText)
                ? SjFoodQuotationResult.WithMessage($"{productName}：{NoMatchingPriceMessage}", customer.SapId)
                : SjFoodQuotationResult.WithMessage($"{productName}：{priceText}", customer.SapId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Query product price failed. Phone: {PhoneNumber}, Hints: {@CustomerHints}, Product: {ProductName}", normalizedPhone, customerHints, productName);
            return SjFoodQuotationResult.WithMessage("The price lookup failed for now. Please ask the customer to try again later or transfer to manual service.");
        }
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;

        return phone
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);
    }
    
    private static GetCustomersPhoneNumberDataDto ResolveCustomer(
        List<GetCustomersPhoneNumberDataDto> customers,
        SjFoodCustomerMatchHints customerHints)
    {
        var validCustomers = customers?
            .Where(x => !string.IsNullOrWhiteSpace(x.SapId))
            .ToList() ?? [];

        if (validCustomers.Count == 0)
            return null;

        if (validCustomers.Count == 1)
            return validCustomers[0];

        if (customerHints?.HasAnyHint != true)
        {
            Log.Information("Multiple CRM customers matched by phone, but no customer hints were provided.");
            return null;
        }

        var matchedCustomers = validCustomers
            .Where(x => MatchesCustomerHints(x, customerHints))
            .ToList();

        if (matchedCustomers.Count == 1)
            return matchedCustomers[0];

        Log.Information("Unable to disambiguate CRM customers by hints {@CustomerHints}. Matched count: {MatchedCount}", customerHints, matchedCustomers.Count);
        return null;
    }

    private static bool MatchesCustomerHints(GetCustomersPhoneNumberDataDto customer, SjFoodCustomerMatchHints hints)
    {
        var normalizedHints = hints.GetCustomerIdentityHints()
            .Select(NormalizeMatchText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (normalizedHints.Count == 0) return false;

        var customerFields = BuildCustomerMatchFields(customer)
            .Select(NormalizeMatchText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return normalizedHints.Any(hint => customerFields.Any(field =>
            field.Contains(hint, StringComparison.OrdinalIgnoreCase)
            || hint.Contains(field, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> BuildCustomerMatchFields(GetCustomersPhoneNumberDataDto customer)
    {
        yield return customer.SapId;
        yield return customer.SapId?.TrimStart('0');
        yield return customer.CustomerName;
        yield return customer.Street;
        yield return customer.Warehouse;
        yield return customer.HeaderNote1;

        if (customer.Contacts == null) yield break;

        foreach (var contact in customer.Contacts)
        {
            yield return contact.Name;
            yield return contact.Identity;
        }
    }

    private static string BuildCustomerDisambiguationMessage(List<GetCustomersPhoneNumberDataDto> customers, SjFoodCustomerMatchHints customerHints)
    {
        var validCustomerCount = customers?.Count(x => !string.IsNullOrWhiteSpace(x.SapId)) ?? 0;

        if (validCustomerCount == 0)
            return MissingCustomerMessage;

        return customerHints?.HasAnyHint == true
            ? MissingCustomerMessage
            : MultipleCustomersMessage;
    }

    private static string NormalizeMatchText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var chars = value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray();

        return chars.Length == 0 ? null : new string(chars);
    }

    private static string FormatQuotation(AiQuotationDto quotation)
    {
        var parts = new List<string>();

        if (quotation.SjAiCost.HasValue)
            parts.Add($"SJ价 {quotation.SjAiCost.Value:0.##}");

        if (quotation.KfOrOsAiCost.HasValue)
            parts.Add($"KF/OS价 {quotation.KfOrOsAiCost.Value:0.##}");

        return string.Join("，", parts);
    }
}

public class SjFoodQuotationResult
{
    public string Message { get; init; }
    public string SapId { get; init; }

    public static SjFoodQuotationResult WithMessage(string message, string sapId = null) => new()
    {
        Message = message,
        SapId = sapId
    };
}

public class SjFoodCustomerMatchHints
{
    public string CustomerHint { get; init; }
    public string SapId { get; init; }
    public string CustomerName { get; init; }
    public string Street { get; init; }
    public string Warehouse { get; init; }
    public string HeaderNote1 { get; init; }
    public string ContactName { get; init; }
    public string ContactIdentity { get; init; }

    public bool HasAnyHint => GetCustomerIdentityHints().Any(x => !string.IsNullOrWhiteSpace(x));

    public IEnumerable<string> GetCustomerIdentityHints()
    {
        yield return CustomerHint;
        yield return SapId;
        yield return SapId?.TrimStart('0');
        yield return CustomerName;
        yield return Street;
        yield return Warehouse;
        yield return HeaderNote1;
        yield return ContactName;
        yield return ContactIdentity;
    }
}
