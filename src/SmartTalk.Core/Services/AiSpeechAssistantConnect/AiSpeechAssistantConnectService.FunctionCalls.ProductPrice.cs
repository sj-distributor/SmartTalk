using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private static string DefaultPriceLine => $"{Random.Shared.Next(1, 21)}元";

    private sealed class ProductPriceArgs
    {
        public string ProductName { get; init; }
        public bool Append { get; init; } = true;
    }

    private async Task<RealtimeAiFunctionCallResult> ProcessProductPriceAsync(
        RealtimeAiWssFunctionCallData functionCallData,
        RealtimeAiSessionActions actions,
        CancellationToken cancellationToken)
    {
        await SendRepeatOrderHoldOnAudioAsync(actions).ConfigureAwait(false);
        
        Log.Information("Get product price for {@productName}", functionCallData?.ArgumentsJson);
        
        var args = ParseProductPriceArgs(functionCallData?.ArgumentsJson);

        if (string.IsNullOrWhiteSpace(args.ProductName))
        {
            Log.Warning("[AiAssistant] get_product_price missing product_name. Raw args: {Args}", functionCallData?.ArgumentsJson);
            return new RealtimeAiFunctionCallResult
            {
                Output = "Missing product_name. Ask the user which product they want the price for."
            };
        }

        var priceLine = DefaultPriceLine;

        Log.Information("Get product price for {@productName}", args);

        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        
        if (_ctx.PriceCache.TryGetValue(args.ProductName, out var cached))
        {
            return new RealtimeAiFunctionCallResult { Output = cached };
        }

        _ctx.PriceCache[args.ProductName] = priceLine;
        
        return new RealtimeAiFunctionCallResult { Output = priceLine };
    }

    private static ProductPriceArgs ParseProductPriceArgs(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return new ProductPriceArgs();

        try
        {
            var token = JToken.Parse(argumentsJson);

            if (token.Type == JTokenType.String)
            {
                var raw = token.Value<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(raw)) return new ProductPriceArgs();

                if (LooksLikeJson(raw))
                    return ParseProductPriceArgs(raw);

                return new ProductPriceArgs { ProductName = raw };
            }

            var append = token.Value<bool?>("append") ?? true;
            var productName = token.Value<string>("product_name")
                              ?? token.Value<string>("productName")
                              ?? token.Value<string>("name")
                              ?? token.Value<string>("product")
                              ?? token.Value<string>("item")
                              ?? token.Value<string>("dish")
                              ?? token.Value<string>("menu_item")
                              ?? token.Value<string>("menuItem")
                              ?? token.Value<string>("title");

            if (string.IsNullOrWhiteSpace(productName))
            {
                productName = token.SelectToken("product.name")?.Value<string>()
                              ?? token.SelectToken("item.name")?.Value<string>()
                              ?? token.SelectToken("dish.name")?.Value<string>()
                              ?? token.SelectToken("menu_item.name")?.Value<string>()
                              ?? token.SelectToken("menuItem.name")?.Value<string>();
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                IEnumerable<JToken> candidates = token is JContainer container
                    ? container.DescendantsAndSelf()
                    : new[] { token };

                var candidate = candidates
                    .OfType<JProperty>()
                    .FirstOrDefault(p => p.Value.Type == JTokenType.String && IsLikelyNameKey(p.Name));

                productName = candidate?.Value?.Value<string>();
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                Log.Warning("[AiAssistant] get_product_price args missing product name: {Args}", argumentsJson);
            }

            return new ProductPriceArgs { ProductName = productName, Append = append };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AiAssistant] Failed to parse get_product_price args: {Args}", argumentsJson);
            return new ProductPriceArgs();
        }
    }

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool IsLikelyNameKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        return key.Contains("name", StringComparison.OrdinalIgnoreCase)
               || key.Contains("product", StringComparison.OrdinalIgnoreCase)
               || key.Contains("item", StringComparison.OrdinalIgnoreCase)
               || key.Contains("dish", StringComparison.OrdinalIgnoreCase);
    }
}
