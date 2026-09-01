using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private const int MaxStoreScopedCustomerItemLines = 150;

    private async Task<RealtimeAiFunctionCallResult> ProcessQueryCustomerItemsByStoreNameAsync(
        RealtimeAiWssFunctionCallData functionCallData,
        RealtimeAiSessionActions actions,
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

        if (match.SoldToIds.Count == 0)
        {
            Log.Information(
                "[AiAssistant] Store name did not match assistant customer scope. AssistantId: {AssistantId}, StoreName: {StoreName}, AssistantCustomerIds: {AssistantCustomerIds}, CrmCustomerIds: {CrmCustomerIds}",
                _ctx.Assistant?.Id, args.StoreName, _ctx.CandidateCustomerIds, match.CrmMatchedSoldToIds);

            return BuildStoreConfirmationRequiredResult(
                "Reply in the guest's language: I could not match that store name to the stores linked to this call. Please ask the customer for the complete or more accurate store name before checking product information.");
        }

        if (match.SoldToIds.Count > 1)
        {
            Log.Information(
                "[AiAssistant] Store name matched multiple assistant customer ids. AssistantId: {AssistantId}, StoreName: {StoreName}, MatchedCustomerIds: {MatchedCustomerIds}",
                _ctx.Assistant?.Id, args.StoreName, match.SoldToIds);

            return BuildStoreConfirmationRequiredResult(
                "Reply in the guest's language: I found more than one store linked to that name. Please ask the customer for the complete or more specific store name before checking product information.");
        }

        var cacheSoldToIds = BuildCustomerItemsCacheSoldToIdCandidates(_ctx.Assistant?.Name, match.SoldToId);
        var caches = await _salesDataProvider
            .GetCustomerItemsCacheBySoldToIdsAsync(cacheSoldToIds, cancellationToken)
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
            "[AiAssistant] Store scoped customer items resolved. AssistantId: {AssistantId}, StoreName: {StoreName}, MatchedCustomerId: {MatchedCustomerId}, ItemLineCount: {ItemLineCount}",
            _ctx.Assistant?.Id, args.StoreName, match.SoldToId, itemLines.Count);

        if (itemLines.Count == 0)
        {
            var promptUpdated = await UpdateCustomerItemsPromptAsync(
                    $"No cached HiFood product information is available for store \"{args.StoreName}\".",
                    actions)
                .ConfigureAwait(false);

            return BuildCustomerItemsPromptUpdatedResult(args.PrefetchOnly, promptUpdated);
        }

        var customerItems = string.Join(Environment.NewLine, itemLines);
        var didUpdatePrompt = await UpdateCustomerItemsPromptAsync(customerItems, actions).ConfigureAwait(false);

        return BuildCustomerItemsPromptUpdatedResult(args.PrefetchOnly, didUpdatePrompt);
    }

    private async Task<bool> UpdateCustomerItemsPromptAsync(string customerItems, RealtimeAiSessionActions actions)
    {
        var updatedPrompt = ReplaceCustomerItemsPromptMarker(customerItems);
        if (updatedPrompt == null)
        {
            Log.Warning(
                "[AiAssistant] Customer items prompt update skipped because no prompt template exists. AssistantId: {AssistantId}, CallSid: {CallSid}",
                _ctx.Assistant?.Id, _ctx.CallSid);
            return false;
        }

        var updatedSessionPrompt = BuildSessionPrompt();
        await actions.UpdateSessionInstructionsAsync(updatedSessionPrompt).ConfigureAwait(false);
        Log.Information(
            "[AiAssistant] Customer items prompt updated in realtime session. AssistantId: {AssistantId}, CallSid: {CallSid}, CustomerItemsLength: {CustomerItemsLength}, UpdatedSessionPrompt: {UpdatedSessionPrompt}",
            _ctx.Assistant?.Id, _ctx.CallSid, _ctx.CustomerItemsPromptValue?.Length ?? 0, updatedSessionPrompt);
        return true;
    }

    private static RealtimeAiFunctionCallResult BuildCustomerItemsPromptUpdatedResult(bool prefetchOnly, bool promptUpdated)
    {
        return new RealtimeAiFunctionCallResult
        {
            Output = promptUpdated
                ? "Customer item knowledge for the confirmed store has been updated in the session instructions."
                : "Customer item knowledge could not be updated because this assistant has no customer_items knowledge placeholder.",
            SuppressResponseAfterOutput = prefetchOnly && promptUpdated
        };
    }

    private static RealtimeAiFunctionCallResult BuildStoreConfirmationRequiredResult(string output) => new()
    {
        Output = output
    };

    internal static List<string> BuildCustomerItemsCacheSoldToIdCandidates(string assistantName, string matchedSoldToId)
    {
        var normalizedMatchedId = NormalizeCustomerId(matchedSoldToId);
        if (string.IsNullOrWhiteSpace(normalizedMatchedId)) return [];

        var candidateIds = (assistantName ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => string.Equals(NormalizeCustomerId(x), normalizedMatchedId, StringComparison.OrdinalIgnoreCase))
            .Append(matchedSoldToId)
            .Append(normalizedMatchedId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidateIds;
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

        [JsonProperty("prefetch_only")]
        public bool PrefetchOnly { get; set; }
    }
}
