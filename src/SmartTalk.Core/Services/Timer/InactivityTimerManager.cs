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
            _timers.TryRemove(sessionId, out _);
        }
    }
}
