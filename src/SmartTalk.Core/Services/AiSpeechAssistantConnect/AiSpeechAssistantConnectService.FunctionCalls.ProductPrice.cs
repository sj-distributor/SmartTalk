using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private const string DefaultPriceLine = "戚风蛋糕:3元";

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
        var priceLine = DefaultPriceLine;

        if (args.Append)
            await AppendPriceToPromptAsync(priceLine, actions).ConfigureAwait(false);

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

    private async Task AppendPriceToPromptAsync(string priceLine, RealtimeAiSessionActions actions)
    {
        if (string.IsNullOrWhiteSpace(priceLine)) return;

        if (!string.IsNullOrEmpty(_ctx.Prompt) && _ctx.Prompt.Contains(priceLine, StringComparison.Ordinal))
            return;

        var updatedPrompt = AppendKnowledgeToPrompt(priceLine);
        _ctx.Prompt = updatedPrompt;

        await actions.UpdateSessionAsync(new RealtimeAiSessionUpdate { Instructions = updatedPrompt }).ConfigureAwait(false);
    }

    private string AppendKnowledgeToPrompt(string knowledge)
    {
        if (string.IsNullOrWhiteSpace(knowledge)) return _ctx.Prompt ?? string.Empty;

        return string.IsNullOrEmpty(_ctx.Prompt)
            ? knowledge
            : $"{_ctx.Prompt}\n\n{knowledge}";
    }
}
