using System.Text;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The external voice path had no tests at all: 759 lines that decide whether a caller hears anything
/// on the assistants routed through MiniMax. It was untestable rather than untested — the provider
/// dialled a real ClientWebSocket, so nothing could stand in for the vendor.
///
/// <para>Everything after the connect now speaks the abstract WebSocket, and a test supplies an
/// already-connected one. That is enough to drive the vendor's handshake and streaming protocol,
/// which is where the terminal signals the engine's turn gate waits on are produced.</para>
/// </summary>
public class MiniMaxRealtimeAiTtsProviderTests
{
    private static RealtimeAiTtsConfig Config() => new()
    {
        ProviderType = RealtimeAiTtsProviderType.MiniMax,
        ServiceUrl = "wss://example.invalid/ws",
        ApiKey = "test-key",
        TargetCodec = RealtimeAiAudioCodec.PCM16,
        SampleRate = 24000
    };

    /// <summary>The vendor's opening exchange: it greets, then acknowledges the task.</summary>
    private static FakeWebSocket VendorSocketAfterHandshake()
    {
        var socket = new FakeWebSocket();

        socket.EnqueueClientMessage("""{"event":"connected_success"}""");
        socket.EnqueueClientMessage("""{"event":"task_started"}""");

        return socket;
    }

    private static MiniMaxRealtimeAiTtsProvider ProviderOver(FakeWebSocket socket)
    {
        var provider = new MiniMaxRealtimeAiTtsProvider();
        provider.WebSocketConnectorOverride = (_, _) => Task.FromResult<System.Net.WebSockets.WebSocket>(socket);

        return provider;
    }

    private static string Sent(FakeWebSocket socket) =>
        string.Join("\n", socket.SentMessages.Select(m => Encoding.UTF8.GetString(m.Data)));

    [Fact]
    public async Task Initialize_ShouldCompleteTheVendorHandshakeAndStartATask()
    {
        // Nothing downstream works if this exchange is wrong: the engine believes the voice path is
        // ready the moment InitializeAsync returns.
        var socket = VendorSocketAfterHandshake();
        var provider = ProviderOver(socket);

        await provider.InitializeAsync(Config(), CancellationToken.None);

        Sent(socket).ShouldContain("task_start");
    }

    [Fact]
    public async Task Initialize_ShouldReportTheConfiguredOutputFormat()
    {
        // The engine resamples provider audio using these two, so a wrong value is audible.
        var provider = ProviderOver(VendorSocketAfterHandshake());

        await provider.InitializeAsync(Config(), CancellationToken.None);

        provider.OutputCodec.ShouldBe(RealtimeAiAudioCodec.PCM16);
        provider.OutputSampleRate.ShouldBe(24000);
    }

    [Fact]
    public async Task VendorClosingBeforeGreeting_ShouldSurfaceRatherThanHang()
    {
        // A silent failure here would leave the engine believing it has a voice.
        var socket = new FakeWebSocket();
        socket.EnqueueClose();

        var provider = ProviderOver(socket);

        await Should.ThrowAsync<InvalidOperationException>(
            () => provider.InitializeAsync(Config(), CancellationToken.None));
    }

    [Fact]
    public async Task TaskFailedDuringHandshake_ShouldSurfaceAsAFailure()
    {
        var socket = new FakeWebSocket();
        socket.EnqueueClientMessage("""{"event":"connected_success"}""");
        socket.EnqueueClientMessage("""{"event":"task_failed"}""");

        var provider = ProviderOver(socket);

        await Should.ThrowAsync<InvalidOperationException>(
            () => provider.InitializeAsync(Config(), CancellationToken.None));
    }

    [Fact]
    public async Task ProviderText_ShouldReachTheVendorAsSynthesisInput()
    {
        var socket = VendorSocketAfterHandshake();
        var provider = ProviderOver(socket);

        await provider.InitializeAsync(Config(), CancellationToken.None);
        await provider.HandleProviderTextDeltaAsync("Your order is ready.", CancellationToken.None);
        await provider.HandleProviderTextDoneAsync(CancellationToken.None);
        await Task.Delay(50);

        Sent(socket).ShouldContain("task_continue");
    }

    /// <summary>
    /// Hands out a fresh vendor socket per dial and can hold one dial open indefinitely, which is how
    /// a test stands in for the seconds a real TCP+TLS+handshake to MiniMax can take.
    /// </summary>
    private sealed class GatedVendorDialer
    {
        private readonly List<FakeWebSocket> _sockets = [];
        private int _dials;

        /// <summary>1-based dial number to block on; dials before it complete immediately.</summary>
        public int BlockFromDial { get; init; } = int.MaxValue;

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<FakeWebSocket> Sockets { get { lock (_sockets) return _sockets.ToList(); } }

        public async Task<System.Net.WebSockets.WebSocket> ConnectAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _dials) >= BlockFromDial)
                await Gate.Task.ConfigureAwait(false);

            var socket = VendorSocketAfterHandshake();
            lock (_sockets) _sockets.Add(socket);

            return socket;
        }
    }

    private static MiniMaxRealtimeAiTtsProvider ProviderOver(GatedVendorDialer dialer) =>
        new() { WebSocketConnectorOverride = dialer.ConnectAsync };

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException($"Timed out after 5s waiting for: {description}");
    }

    [Fact]
    public async Task Interrupt_ShouldNotBlockOnTheReopenHandshake()
    {
        // Barge-in is awaited from the engine's provider event loop (RealtimeAiService.Event.cs:255),
        // so anything slow here stalls EVERY provider event — transcripts, response starts, errors —
        // for its whole duration. Closing is what silences the vendor; redialling is preparation for
        // the next turn and has no reason to hold the loop.
        var dialer = new GatedVendorDialer { BlockFromDial = 2 };
        var provider = ProviderOver(dialer);

        await provider.InitializeAsync(Config(), CancellationToken.None);

        await Should.NotThrowAsync(() => provider.HandleInterruptAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)));

        dialer.Gate.SetResult();
    }

    [Fact]
    public async Task TextArrivingWhileTheReopenIsStillDialling_ShouldReachTheVendorOnceItIsUp()
    {
        // The safety net for not waiting on the redial: the next turn's text must not be dropped in
        // the window where there is no socket. QueueOrSendSegmentAsync already queues for a not-ready
        // socket and OpenConnectionAsync flushes on the way out — this pins that the two meet.
        var dialer = new GatedVendorDialer { BlockFromDial = 2 };
        var provider = ProviderOver(dialer);

        await provider.InitializeAsync(Config(), CancellationToken.None);
        await provider.HandleInterruptAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        await provider.HandleProviderTextDeltaAsync("Your order is ready.", CancellationToken.None);
        await provider.HandleProviderTextDoneAsync(CancellationToken.None);

        dialer.Gate.SetResult();

        await WaitUntilAsync(
            () => dialer.Sockets.Count == 2 && Sent(dialer.Sockets[1]).Contains("task_continue"),
            "the text queued during the redial to reach the reopened vendor socket");
    }

    [Fact]
    public async Task StopWhileTheReopenIsStillDialling_ShouldNotLeaveAVendorSocketOpen()
    {
        // The reopen outliving its session is the risk that comes with not awaiting it. Stopping must
        // either pre-empt the redial or close what it opened — never leave a live socket behind.
        var dialer = new GatedVendorDialer { BlockFromDial = 2 };
        var provider = ProviderOver(dialer);

        await provider.InitializeAsync(Config(), CancellationToken.None);
        await provider.HandleInterruptAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        var stop = provider.StopAsync(CancellationToken.None);
        dialer.Gate.SetResult();

        await stop.WaitAsync(TimeSpan.FromSeconds(5));

        await WaitUntilAsync(
            () => dialer.Sockets.All(s => s.State != System.Net.WebSockets.WebSocketState.Open),
            "every vendor socket to be closed after StopAsync");
    }

    [Fact]
    public async Task Stop_ShouldCloseTheVendorSocket()
    {
        // Left open, the socket leaks for the lifetime of the DI scope.
        var socket = VendorSocketAfterHandshake();
        var provider = ProviderOver(socket);

        await provider.InitializeAsync(Config(), CancellationToken.None);
        await provider.StopAsync(CancellationToken.None);

        socket.State.ShouldNotBe(System.Net.WebSockets.WebSocketState.Open);
    }
}
