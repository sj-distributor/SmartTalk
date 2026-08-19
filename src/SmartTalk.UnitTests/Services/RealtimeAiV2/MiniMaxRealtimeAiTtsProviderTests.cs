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
