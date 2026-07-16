using Newtonsoft.Json;
using Serilog;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private const int MaxStoreScopedCustomerItemLines = 150;

    private async Task<RealtimeAiFunctionCallResult> ProcessQueryCustomerItemsByStoreNameAsync(
        RealtimeAiWssFunctionCallData functionCallData,
        CancellationToken cancellationToken)
    {
        var args = ParseQueryCustomerItemsArguments(functionCallData.ArgumentsJson);
        if (string.IsNullOrWhiteSpace(args.StoreName))
        {
            return new RealtimeAiFunctionCallResult
            {
                Output = "Reply in the guest's language: Please ask the customer which store or restaurant name they are asking about before checking HiFood product information."
            };
        }

        _ctx.CandidateCustomerIds = SplitAssistantCustomerIds(_ctx.Assistant?.Name);

        var match = await _salesCustomerMatchService
            .MatchStoreNameInCustomerScopeAsync(_ctx.Assistant?.Name, args.StoreName, cancellationToken)
            .ConfigureAwait(false);

        _ctx.MatchedCustomerIds = match.SoldToIds;

        if (match.SoldToIds.Count == 0)
        {
            Log.Information(
                "[AiAssistant] Store name did not match assistant customer scope. AssistantId: {AssistantId}, StoreName: {StoreName}, AssistantCustomerIds: {AssistantCustomerIds}, CrmCustomerIds: {CrmCustomerIds}",
                _ctx.Assistant?.Id, args.StoreName, _ctx.CandidateCustomerIds, match.CrmMatchedSoldToIds);

            return new RealtimeAiFunctionCallResult
            {
                Output = "Reply in the guest's language: I could not match that store name to the stores linked to this call. Please ask the customer for another store name, address, phone number, or contact name before answering product stock or warehouse questions."
            };
        }

        var caches = await _salesDataProvider
            .GetCustomerItemsCacheBySoldToIdsAsync(match.SoldToIds, cancellationToken)
            .ConfigureAwait(false);

        var itemLines = caches
            .Where(x => !string.IsNullOrWhiteSpace(x.CacheValue))
            .SelectMany(x => x.CacheValue.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxStoreScopedCustomerItemLines)
            .ToList();

        Log.Information(
            "[AiAssistant] Store scoped customer items resolved. AssistantId: {AssistantId}, StoreName: {StoreName}, ProductName: {ProductName}, MatchedCustomerIds: {MatchedCustomerIds}, ItemLineCount: {ItemLineCount}",
            _ctx.Assistant?.Id, args.StoreName, args.ProductName, match.SoldToIds, itemLines.Count);

        if (itemLines.Count == 0)
        {
            return new RealtimeAiFunctionCallResult
            {
                Output = $"Matched customer IDs for store \"{args.StoreName}\": {string.Join(", ", match.SoldToIds)}.\nNo cached HiFood product information was found for these customer IDs. Reply in the guest's language and explain that no product data is available for this store right now."
            };
        }

        return new RealtimeAiFunctionCallResult
        {
            Output =
                $"Store name: {args.StoreName}\n" +
                $"Customer IDs matched within this assistant: {string.Join(", ", match.SoldToIds)}\n" +
                $"Customer IDs returned by CRM for this store name: {string.Join(", ", match.CrmMatchedSoldToIds)}\n" +
                $"Requested product name or alias: {args.ProductName ?? string.Empty}\n" +
                $"HiFood product information scoped to the matched customer IDs, limited to {MaxStoreScopedCustomerItemLines} lines:\n" +
                string.Join(Environment.NewLine, itemLines) +
                "\n\nReply in the guest's language. Use only the scoped product information above. Do not mix in products from other customer IDs."
        };
    }

    private static QueryCustomerItemsByStoreNameArguments ParseQueryCustomerItemsArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return new QueryCustomerItemsByStoreNameArguments();

        try
        {
            return JsonConvert.DeserializeObject<QueryCustomerItemsByStoreNameArguments>(argumentsJson) ?? new QueryCustomerItemsByStoreNameArguments();
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Failed to parse query customer items arguments: {ArgumentsJson}", argumentsJson);
            return new QueryCustomerItemsByStoreNameArguments();
        }
    }

    private sealed class QueryCustomerItemsByStoreNameArguments
    {
        [JsonProperty("store_name")]
        public string StoreName { get; set; }

        [JsonProperty("product_name")]
        public string ProductName { get; set; }
    }
}
