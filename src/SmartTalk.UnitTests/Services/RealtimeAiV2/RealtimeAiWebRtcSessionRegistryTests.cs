using Microsoft.Extensions.DependencyInjection;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiWebRtcSessionRegistryTests
{
    [Fact]
    public async Task Create_RequestCancellation_CancelsInitialization()
    {
        var session = new ControllableSession { WaitForInitializationCancellation = true };
        using var provider = BuildServiceProvider(session);
        using var registry = new RealtimeAiWebRtcSessionRegistry(
            provider.GetRequiredService<IServiceScopeFactory>());
        using var requestCts = new CancellationTokenSource();

        var createTask = registry.CreateAsync(
            625,
            RealtimeAiServerRegion.HK,
            "offer",
            requestCts.Token);

        await session.InitializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.NotEqual(session.InitializationToken, session.SessionToken);

        requestCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            createTask.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.True(session.InitializationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Stop_WaitsForReadyCallbackBeforeDisposingScopedSession()
    {
        var session = new ControllableSession { BlockReadyCallback = true };
        using var provider = BuildServiceProvider(session);
        using var registry = new RealtimeAiWebRtcSessionRegistry(
            provider.GetRequiredService<IServiceScopeFactory>());

        var call = await registry.CreateAsync(
            625,
            RealtimeAiServerRegion.HK,
            "offer",
            CancellationToken.None);
        var readyTask = registry.MarkClientReadyAsync(call.CallId);
        await session.ReadyCallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var stopTask = registry.StopAsync(call.CallId);
        var earlyDisposal = await Task.WhenAny(
            session.Disposed.Task,
            Task.Delay(TimeSpan.FromMilliseconds(200)));

        Assert.NotSame(session.Disposed.Task, earlyDisposal);

        session.ReleaseReadyCallback.TrySetResult();

        Assert.True(await readyTask.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.True(await stopTask.WaitAsync(TimeSpan.FromSeconds(1)));
        await session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AppendRecording_ForwardsPcmBeforeStopAndRejectsItWhileStopping()
    {
        var session = new ControllableSession { BlockRunCleanup = true };
        using var provider = BuildServiceProvider(session);
        using var registry = new RealtimeAiWebRtcSessionRegistry(
            provider.GetRequiredService<IServiceScopeFactory>());
        var call = await registry.CreateAsync(
            625,
            RealtimeAiServerRegion.HK,
            "offer",
            CancellationToken.None);
        var pcmBytes = new byte[] { 1, 0, 2, 0 };

        var accepted = await registry.AppendRecordingAsync(call.CallId, 0, pcmBytes, false);
        Assert.Equal(RealtimeAiWebRtcRecordingAppendStatus.Accepted, accepted.Status);
        Assert.Equal(pcmBytes, session.RecordedPcm);

        var stopTask = registry.StopAsync(call.CallId);
        await session.RunCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var rejected = await registry.AppendRecordingAsync(call.CallId, 1, pcmBytes, false);
        Assert.Equal(RealtimeAiWebRtcRecordingAppendStatus.NotFound, rejected.Status);

        session.ReleaseRunCleanup.TrySetResult();
        Assert.True(await stopTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task AppendRecording_FinalChunkStopsSessionAfterItIsAccepted()
    {
        var session = new ControllableSession { BlockRunCleanup = true };
        using var provider = BuildServiceProvider(session);
        using var registry = new RealtimeAiWebRtcSessionRegistry(
            provider.GetRequiredService<IServiceScopeFactory>());
        var call = await registry.CreateAsync(
            625,
            RealtimeAiServerRegion.HK,
            "offer",
            CancellationToken.None);

        var result = await registry.AppendRecordingAsync(
            call.CallId,
            0,
            new byte[] { 1, 0 },
            true);

        Assert.Equal(RealtimeAiWebRtcRecordingAppendStatus.Accepted, result.Status);
        await session.RunCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

        session.ReleaseRunCleanup.TrySetResult();
        await session.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static ServiceProvider BuildServiceProvider(IAiKidRealtimeWebRtcSession session)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => session);
        return services.BuildServiceProvider();
    }

    private sealed class ControllableSession : IAiKidRealtimeWebRtcSession, IDisposable
    {
        public bool WaitForInitializationCancellation { get; init; }

        public bool BlockReadyCallback { get; init; }

        public bool BlockRunCleanup { get; init; }

        public CancellationToken InitializationToken { get; private set; }

        public CancellationToken SessionToken { get; private set; }

        public TaskCompletionSource InitializationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReadyCallbackStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseReadyCallback { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Disposed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource RunCancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseRunCleanup { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public byte[] RecordedPcm { get; private set; } = [];

        public async Task<RealtimeAiWebRtcCallResult> InitializeAsync(
            int assistantId,
            RealtimeAiServerRegion region,
            string offerSdp,
            CancellationToken initializationCancellationToken,
            CancellationToken sessionCancellationToken)
        {
            InitializationToken = initializationCancellationToken;
            SessionToken = sessionCancellationToken;
            InitializationStarted.TrySetResult();

            if (WaitForInitializationCancellation)
                await Task.Delay(Timeout.InfiniteTimeSpan, initializationCancellationToken);

            return new RealtimeAiWebRtcCallResult
            {
                CallId = "rtc_registry_test",
                AnswerSdp = "answer"
            };
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                RunCancellationObserved.TrySetResult();
                if (BlockRunCleanup)
                    await ReleaseRunCleanup.Task;
            }
        }

        public async Task MarkClientReadyAsync()
        {
            ReadyCallbackStarted.TrySetResult();
            if (BlockReadyCallback)
                await ReleaseReadyCallback.Task;
        }

        public Task<AppendRealtimeAiWebRtcRecordingResponse> AppendRecordingAsync(
            long sequence,
            ReadOnlyMemory<byte> pcmBytes,
            bool isFinal)
        {
            RecordedPcm = pcmBytes.ToArray();
            return Task.FromResult(new AppendRealtimeAiWebRtcRecordingResponse
            {
                Status = RealtimeAiWebRtcRecordingAppendStatus.Accepted,
                NextSequence = sequence + 1
            });
        }

        public void Dispose()
        {
            Disposed.TrySetResult();
        }
    }
}
