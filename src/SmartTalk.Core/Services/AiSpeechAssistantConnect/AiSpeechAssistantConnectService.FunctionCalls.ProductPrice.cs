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
        var args = ParseProductPriceArgs(functionCallData?.ArgumentsJson);
        await actions.SendTextToProviderAsync("正在为您查询价格，请稍等").ConfigureAwait(false);
        var priceLine = DefaultPriceLine;
        
        Log.Information("Get product price for {productName}", args.ProductName);
        
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        
        if (!string.IsNullOrWhiteSpace(args.ProductName) &&
            _ctx.PriceCache.TryGetValue(args.ProductName, out var cached))
        {
            return new RealtimeAiFunctionCallResult { Output = cached };
        }

        if (!string.IsNullOrWhiteSpace(args.ProductName))
            _ctx.PriceCache[args.ProductName] = priceLine;

        return new RealtimeAiFunctionCallResult { Output = priceLine };
    }

    private static ProductPriceArgs ParseProductPriceArgs(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return new ProductPriceArgs();

        try
        {
            var obj = JObject.Parse(argumentsJson);
            return new ProductPriceArgs
            {
                ProductName = obj.Value<string>("product_name")
                              ?? obj.Value<string>("productName")
                              ?? obj.Value<string>("name"),
                Append = obj.Value<bool?>("append") ?? true
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AiAssistant] Failed to parse get_product_price args: {Args}", argumentsJson);
            return new ProductPriceArgs();
        }
    }

}
