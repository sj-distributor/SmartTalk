using Microsoft.Extensions.DependencyInjection;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
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
            }
        }

        public async Task MarkClientReadyAsync()
        {
            ReadyCallbackStarted.TrySetResult();
            if (BlockReadyCallback)
                await ReleaseReadyCallback.Task;
        }

        public void Dispose()
        {
            Disposed.TrySetResult();
        }
    }
}
