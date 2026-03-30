using Serilog;
using SmartTalk.Core.Settings.OpenAi;

namespace SmartTalk.Core.Extensions;

public static class OpenAiSettingsExtension
{
    public static async Task<T> ExecuteWithApiKeyFailoverAsync<T>(
        this OpenAiSettings settings,
        Func<string, Task<T>> executeAsync,
        Func<T, bool> isSuccess = null,
        bool isHk = false,
        string operationName = "OpenAI request",
        bool throwIfAllFailed = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(executeAsync);

        var candidates = settings.GetApiKeyCandidates(isHk);
        if (candidates.Count == 0)
        {
            var message = $"{operationName}: no OpenAI api keys configured.";

            Log.Error(message);

            if (throwIfAllFailed)
                throw new InvalidOperationException(message);

            return default;
        }

        var success = isSuccess ?? (result => result != null);
        Exception lastException = null;

        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await executeAsync(candidates[index]).ConfigureAwait(false);

                if (success(result))
                {
                    if (index > 0)
                        Log.Information("{OperationName}: fallback to backup OpenAI key succeeded at index {Index}.", operationName, index);

                    return result;
                }

                Log.Warning("{OperationName}: OpenAI response was invalid with key index {Index}, trying next key.", operationName, index);
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log.Warning(ex, "{OperationName}: OpenAI call failed with key index {Index}, trying next key.", operationName, index);
            }
        }

        if (throwIfAllFailed)
        {
            if (lastException != null)
                throw new InvalidOperationException($"{operationName}: all OpenAI keys failed.", lastException);

            throw new InvalidOperationException($"{operationName}: all OpenAI keys returned invalid results.");
        }

        return default;
    }
}
