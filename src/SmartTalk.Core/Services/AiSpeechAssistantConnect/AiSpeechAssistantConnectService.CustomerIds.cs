namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private static List<string> SplitAssistantCustomerIds(string assistantName)
    {
        if (string.IsNullOrWhiteSpace(assistantName)) return [];

        return assistantName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeCustomerId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeCustomerId(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return string.Empty;

        var normalized = customerId.Trim().TrimStart('0');
        return string.IsNullOrWhiteSpace(normalized) ? "0" : normalized;
    }
}
