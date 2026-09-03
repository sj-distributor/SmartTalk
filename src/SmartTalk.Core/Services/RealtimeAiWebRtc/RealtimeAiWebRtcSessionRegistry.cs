using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;
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

    Task<AppendRealtimeAiWebRtcRecordingResponse> AppendRecordingAsync(
        string callId,
        long sequence,
        ReadOnlyMemory<byte> pcmBytes,
        bool isFinal);

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

    public async Task<AppendRealtimeAiWebRtcRecordingResponse> AppendRecordingAsync(
        string callId,
        long sequence,
        ReadOnlyMemory<byte> pcmBytes,
        bool isFinal)
    {
        if (!_sessions.TryGetValue(callId, out var active))
            return new AppendRealtimeAiWebRtcRecordingResponse
            {
                Status = RealtimeAiWebRtcRecordingAppendStatus.NotFound
            };

        var result = await active.AppendRecordingAsync(sequence, pcmBytes, isFinal).ConfigureAwait(false);
        if (isFinal && result.Status is (
                RealtimeAiWebRtcRecordingAppendStatus.Accepted or
                RealtimeAiWebRtcRecordingAppendStatus.Duplicate))
            await active.RequestStopAsync().ConfigureAwait(false);

        return result;
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
        private bool _stopping;

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

        public async Task<AppendRealtimeAiWebRtcRecordingResponse> AppendRecordingAsync(
            long sequence,
            ReadOnlyMemory<byte> pcmBytes,
            bool isFinal)
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed || _stopping)
                    return new AppendRealtimeAiWebRtcRecordingResponse
                    {
                        Status = RealtimeAiWebRtcRecordingAppendStatus.NotFound
                    };

                return await Session.AppendRecordingAsync(sequence, pcmBytes, isFinal).ConfigureAwait(false);
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
                if (_disposed || _stopping) return false;

                _stopping = true;
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

        public async Task RequestStopAsync()
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed || _stopping) return;

                _stopping = true;
                Cancellation.Cancel();
            }
            finally
            {
                _lifecycleGate.Release();
            }
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
