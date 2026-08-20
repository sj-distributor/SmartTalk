using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiWebRtc;

public interface IRealtimeAiWebRtcSessionRegistry : ISingletonDependency
{
    Task<RealtimeAiWebRtcCallResult> CreateAsync(
        int assistantId,
        RealtimeAiServerRegion region,
        string offerSdp,
        CancellationToken cancellationToken);

    Task<bool> MarkClientReadyAsync(string callId);

    Task<bool> StopAsync(string callId);
}

public sealed class RealtimeAiWebRtcSessionRegistry : IRealtimeAiWebRtcSessionRegistry, IDisposable
{
    private static readonly TimeSpan TestLinkMaxSessionDuration = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, ActiveSession> _sessions = new();

    public RealtimeAiWebRtcSessionRegistry(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<RealtimeAiWebRtcCallResult> CreateAsync(
        int assistantId,
        RealtimeAiServerRegion region,
        string offerSdp,
        CancellationToken cancellationToken)
    {
        var scope = _scopeFactory.CreateScope();
        var sessionCts = new CancellationTokenSource(TestLinkMaxSessionDuration);

        try
        {
            var session = scope.ServiceProvider.GetRequiredService<IAiKidRealtimeWebRtcSession>();
            using var initializationCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionCts.Token);
            var result = await session.InitializeAsync(
                assistantId,
                region,
                offerSdp,
                initializationCts.Token,
                sessionCts.Token).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var active = new ActiveSession(scope, session, sessionCts);
            if (!_sessions.TryAdd(result.CallId, active))
                throw new InvalidOperationException($"Duplicate WebRTC call ID: {result.CallId}.");

            active.Completion = Task.Run(() => RunAndCleanupAsync(result.CallId, active));
            return result;
        }
        catch
        {
            sessionCts.Cancel();
            sessionCts.Dispose();
            scope.Dispose();
            throw;
        }
    }

    public async Task<bool> MarkClientReadyAsync(string callId)
    {
        if (!_sessions.TryGetValue(callId, out var active)) return false;

        return await active.MarkClientReadyAsync().ConfigureAwait(false);
    }

    public async Task<bool> StopAsync(string callId)
    {
        if (!_sessions.TryGetValue(callId, out var active)) return false;

        return await active.StopAsync().ConfigureAwait(false);
    }

    private async Task RunAndCleanupAsync(string callId, ActiveSession active)
    {
        try
        {
            await active.Session.RunAsync(active.Cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            _sessions.TryRemove(callId, out _);
            await active.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        foreach (var active in _sessions.Values)
            active.Cancel();
    }

    private sealed class ActiveSession
    {
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private bool _disposed;

        public ActiveSession(
            IServiceScope scope,
            IAiKidRealtimeWebRtcSession session,
            CancellationTokenSource cancellation)
        {
            Scope = scope;
            Session = session;
            Cancellation = cancellation;
        }

        public IServiceScope Scope { get; }

        public IAiKidRealtimeWebRtcSession Session { get; }

        public CancellationTokenSource Cancellation { get; }

        public Task Completion { get; set; }

        public async Task<bool> MarkClientReadyAsync()
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return false;

                await Session.MarkClientReadyAsync().ConfigureAwait(false);
                return true;
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public async Task<bool> StopAsync()
        {
            Task completion;

            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return false;

                Cancellation.Cancel();
                completion = Completion;
            }
            finally
            {
                _lifecycleGate.Release();
            }

            if (completion != null)
                await completion.ConfigureAwait(false);

            return true;
        }

        public void Cancel()
        {
            try
            {
                Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The background cleanup won the shutdown race.
            }
        }

        public async Task DisposeAsync()
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return;

                _disposed = true;
                Cancellation.Dispose();
                Scope.Dispose();
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
    }
}
