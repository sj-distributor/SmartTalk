using System.Collections.Concurrent;
using Serilog;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Timer;

public interface IInactivityTimerManager : IScopedDependency
{
    void StartTimer(string sessionId, TimeSpan timeout, Func<Task> onTimeout);

    void StopTimer(string sessionId);

    void ResetTimer(string sessionId);

    bool IsTimerRunning(string sessionId);
}

public class InactivityTimerManager : IInactivityTimerManager
{
    private class TimerEntry
    {
        public required CancellationTokenSource Cts { get; init; }
        public required TimeSpan Timeout { get; init; }
        public required Func<Task> OnTimeout { get; init; }
    }

    private readonly ConcurrentDictionary<string, TimerEntry> _timers = new();

    public void StartTimer(string sessionId, TimeSpan timeout, Func<Task> onTimeout)
    {
        StopTimer(sessionId);

        var cts = new CancellationTokenSource();
        var entry = new TimerEntry { Cts = cts, Timeout = timeout, OnTimeout = onTimeout };

        _timers[sessionId] = entry;

        _ = RunTimerAsync(sessionId, entry);
    }

    public void StopTimer(string sessionId)
    {
        if (!_timers.TryRemove(sessionId, out var entry)) return;

        try
        {
            entry.Cts.Cancel();
            entry.Cts.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    public void ResetTimer(string sessionId)
    {
        if (!_timers.TryGetValue(sessionId, out var entry)) return;

        StartTimer(sessionId, entry.Timeout, entry.OnTimeout);
    }

    public bool IsTimerRunning(string sessionId) => _timers.ContainsKey(sessionId);

    private async Task RunTimerAsync(string sessionId, TimerEntry entry)
    {
        try
        {
            await Task.Delay(entry.Timeout, entry.Cts.Token).ConfigureAwait(false);
            
            // The timer may have been replaced for the same sessionId while this task
            // was waiting. Only the current active entry is allowed to invoke callback.
            if (!_timers.TryGetValue(sessionId, out var activeEntry) || !ReferenceEquals(activeEntry, entry)) return;
            
            await entry.OnTimeout().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException)
        {
            Log.Warning("[InactivityTimer] Callback skipped, scope already disposed, SessionId: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[InactivityTimer] Callback error, SessionId: {SessionId}", sessionId);
        }
        finally
        {
            // Remove only if this exact entry is still mapped. Avoid deleting a newer
            // timer that might have been started for the same session in the meantime.
            _timers.TryRemove(new KeyValuePair<string, TimerEntry>(sessionId, entry));
        }
    }
}
