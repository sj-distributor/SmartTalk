using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;
using System.Linq;
using System.Text;

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
        await SendPriceLookupHoldOnAudioAsync(actions, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get product price for {@productName}", functionCallData?.ArgumentsJson);
        
        var args = ParseProductPriceArgs(functionCallData?.ArgumentsJson);

        var priceLine = DefaultPriceLine;

        Log.Information("Get product price for {@productName}", args);

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

    private async Task SendPriceLookupHoldOnAudioAsync(
        RealtimeAiSessionActions actions,
        CancellationToken cancellationToken)
    {
        try
        {
            const string holdOnText = "正在为您查询价格，请稍等";

            var responseAudio = await _openaiClient.GenerateSpeechAsync(
                holdOnText,
                _ctx.Assistant.ModelVoice,
                cancellationToken).ConfigureAwait(false);

            if (responseAudio is not { Length: > 0 })
                return;

            responseAudio = NormalizeWavHeader(responseAudio);

            var uLawAudioBytes = await _ffmpegService
                .ConvertWavToULawAsync(responseAudio, cancellationToken)
                .ConfigureAwait(false);

            await actions
                .SendAudioToClientAsync(Convert.ToBase64String(uLawAudioBytes))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AiAssistant] Failed to send price lookup TTS.");
        }
    }

    private static byte[] NormalizeWavHeader(byte[] wavBytes)
    {
        if (wavBytes is not { Length: >= 44 })
            return wavBytes;

        if (!IsAsciiAt(wavBytes, 0, "RIFF") || !IsAsciiAt(wavBytes, 8, "WAVE"))
            return wavBytes;

        // Fix RIFF chunk size
        WriteInt32LE(wavBytes, 4, wavBytes.Length - 8);

        // Fix data chunk size if present
        var index = 12;
        while (index + 8 <= wavBytes.Length)
        {
            var chunkId = Encoding.ASCII.GetString(wavBytes, index, 4);
            var chunkSize = BitConverter.ToInt32(wavBytes, index + 4);
            var nextIndex = index + 8 + Math.Max(chunkSize, 0);

            if (chunkId == "data")
            {
                var dataSize = wavBytes.Length - (index + 8);
                if (chunkSize != dataSize)
                    WriteInt32LE(wavBytes, index + 4, dataSize);
                break;
            }

            if (nextIndex <= index + 8 || nextIndex > wavBytes.Length)
                break;

            index = nextIndex;
            if ((chunkSize & 1) == 1)
                index += 1;
        }

        return wavBytes;
    }

    private static bool IsAsciiAt(byte[] buffer, int offset, string value)
    {
        if (offset < 0 || offset + value.Length > buffer.Length)
            return false;

        for (var i = 0; i < value.Length; i++)
        {
            if (buffer[offset + i] != (byte)value[i])
                return false;
        }

        return true;
    }

    private static void WriteInt32LE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

}
