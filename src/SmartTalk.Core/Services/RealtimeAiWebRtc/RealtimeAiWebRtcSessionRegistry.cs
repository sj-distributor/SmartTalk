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
            var result = await session.InitializeAsync(
                assistantId,
                region,
                offerSdp,
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

        await active.Session.MarkClientReadyAsync().ConfigureAwait(false);
        return true;
    }

    public async Task<bool> StopAsync(string callId)
    {
        if (!_sessions.TryGetValue(callId, out var active)) return false;

        active.Cancellation.Cancel();
        if (active.Completion != null)
            await active.Completion.ConfigureAwait(false);

        return true;
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
            active.Cancellation.Dispose();
            active.Scope.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var active in _sessions.Values)
            active.Cancellation.Cancel();
    }

    private sealed class ActiveSession
    {
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
    }
}
