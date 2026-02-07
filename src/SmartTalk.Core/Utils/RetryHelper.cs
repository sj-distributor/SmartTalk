using Serilog;

namespace SmartTalk.Core.Utils;

public static class RetryHelper
{
    /// <summary>
    /// Retries an async action with fixed delay on exception.
    /// </summary>
    public static async Task RetryAsync(
        Func<Task> action,
        int maxRetryCount,
        int delaySeconds,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt <= maxRetryCount && !cancellationToken.IsCancellationRequested)
            {
                Log.Warning(ex, "Retry attempt {Attempt}/{MaxRetry} failed, retrying in {Delay}s…", attempt, maxRetryCount, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Retries an async action that returns a value, with fixed delay on exception.
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> action,
        int maxRetryCount,
        int delaySeconds,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt <= maxRetryCount && !cancellationToken.IsCancellationRequested)
            {
                Log.Warning(ex, "Retry attempt {Attempt}/{MaxRetry} failed, retrying in {Delay}s…", attempt, maxRetryCount, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Executes an async operation and retries when the result matches a condition, with configurable delay.
    /// </summary>
    public static async Task<T> RetryOnResultAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<T, bool> shouldRetry,
        int maxRetryCount = 1,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(10);
        var result = await operation(cancellationToken).ConfigureAwait(false);

        for (var attempt = 0; attempt < maxRetryCount && shouldRetry(result); attempt++)
        {
            await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            result = await operation(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}