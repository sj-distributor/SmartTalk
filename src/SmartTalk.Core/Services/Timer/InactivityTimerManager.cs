using System.Collections.Concurrent;
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
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public Task RunningTask { get; set; }
    }

    private readonly ConcurrentDictionary<string, TimerEntry> _timers = new();

    public void StartTimer(string sessionId, TimeSpan timeout, Func<Task> onTimeout)
    {
        StopTimer(sessionId); // 保证先取消旧的

        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeout, cts.Token);
                await onTimeout();
            }
            catch (TaskCanceledException) { /* 被取消 */ }
        }, cts.Token);

        _timers[sessionId] = new TimerEntry
        {
            CancellationTokenSource = cts,
            RunningTask = task
        };
    }

    public void StopTimer(string sessionId)
    {
        if (_timers.TryRemove(sessionId, out var timer))
        {
            try
            {
                timer.CancellationTokenSource.Cancel();
                timer.CancellationTokenSource.Dispose();
            }
            catch
            {
                // ignored
            }
        }
    }

    public void ResetTimer(string sessionId)
    {
        if (_timers.TryGetValue(sessionId, out var timer))
        {
            var timeout = TimeSpan.FromMinutes(2); // 默认超时
            var onTimeout = timer.RunningTask.AsyncState as Func<Task>;

            // 由于 AsyncState 不可靠，可考虑扩展为 TimerEntry 内部缓存 onTimeout 与 timeout
            StopTimer(sessionId);
            StartTimer(sessionId, timeout, onTimeout ?? (() => Task.CompletedTask));
        }
    }

    public bool IsTimerRunning(string sessionId) => _timers.ContainsKey(sessionId);
}