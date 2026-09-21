using System.Net.WebSockets;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// CHARACTERIZATION — <see cref="RealtimeSessionOptions.MaxSessionDuration"/> is the engine's only
/// hard session kill-switch, and it had no coverage at all. It cancels the session CTS mid-frame
/// (RealtimeAiService.Context.cs:34), which every send, every lock acquisition, and the client read
/// loop all observe, so the interesting question is not that it fires but what survives it.
///
/// <para>These tests exist because the hardening plan's cleanup changes (moving cleanup into a
/// finally, adding a done-latch) run straight through this path. Without them, a regression that
/// loses a call's transcript on timeout would be invisible.</para>
///
/// <para>Only AiKid TestLink sets this option today (AiKidRealtimeServiceV2.cs:95); the phone-call
/// consumer leaves it null, so the phone path relies entirely on the caller hanging up.</para>
/// </summary>
public class RealtimeAiServiceMaxSessionDurationTests : RealtimeAiServiceTestBase
{
    private static readonly TimeSpan ShortDuration = TimeSpan.FromMilliseconds(200);

    private static ParsedRealtimeAiProviderEvent CompletedUserTranscript(string text) =>
        new()
        {
            Type = RealtimeAiWssEventType.InputAudioTranscriptionCompleted,
            Data = new RealtimeAiWssTranscriptionData { Transcript = text, Speaker = AiSpeechAssistantSpeaker.User }
        };

    [Fact]
    public async Task MaxSessionDurationElapses_ShouldEndSessionAndCloseClientWebSocket()
    {
        var options = CreateDefaultOptions(o => o.MaxSessionDuration = ShortDuration);

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        FakeWs.State.ShouldBe(WebSocketState.Closed,
            "the duration ceiling must close the client socket, not leave it dangling until the platform idle timeout");
    }

    [Fact]
    public async Task MaxSessionDurationElapses_ShouldStillInvokeSessionEndedCallback()
    {
        string? endedSessionId = null;
        var options = CreateDefaultOptions(o =>
        {
            o.MaxSessionDuration = ShortDuration;
            o.OnSessionEndedAsync = id => { endedSessionId = id; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        endedSessionId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task MaxSessionDurationElapsesMidSession_ShouldStillFlushTranscriptions()
    {
        // The transcript is buffered in memory for the whole call and flushed once at teardown.
        // A timeout that skipped the flush would silently lose the entire conversation.
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(_ => CompletedUserTranscript("hello"));

        List<(AiSpeechAssistantSpeaker Speaker, string Text)>? flushed = null;
        var options = CreateDefaultOptions(o =>
        {
            o.MaxSessionDuration = ShortDuration;
            o.OnTranscriptionsCompletedAsync = (_, t) => { flushed = t.ToList(); return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("transcript");

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        flushed.ShouldNotBeNull();
        flushed.ShouldHaveSingleItem().Text.ShouldBe("hello");
    }

    [Fact]
    public async Task MaxSessionDurationElapses_ShouldDisconnectFromProvider()
    {
        var options = CreateDefaultOptions(o => o.MaxSessionDuration = ShortDuration);

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        FakeWssClient.DisconnectCallCount.ShouldBe(1,
            "leaving the provider socket open after a ceiling-triggered teardown would keep the session billable");
    }

    [Theory]
    [InlineData(null)]           // option not set — the phone-call consumer's configuration
    [InlineData(0)]              // explicitly zero — ApplyMaxSessionDurationIfRequired treats it as "off"
    public async Task MaxSessionDurationNotArmed_ShouldLeaveSessionRunning(int? milliseconds)
    {
        var options = CreateDefaultOptions(o =>
            o.MaxSessionDuration = milliseconds.HasValue ? TimeSpan.FromMilliseconds(milliseconds.Value) : null);

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await Task.Delay(ShortDuration * 3);

        sessionTask.IsCompleted.ShouldBeFalse("no ceiling was armed, so only the client hanging up may end the session");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Pins a quirk worth seeing change: on an ABNORMAL exit (the read loop threw rather than
    /// receiving a close frame) the engine only closes the client socket when MaxSessionDuration
    /// happens to be configured — <c>CleanupSessionAsync</c> gates the close on that unrelated
    /// option (RealtimeAiService.Orchestration.cs:131). For the phone path, which never sets it,
    /// an abnormal exit therefore leaves the socket to the consumer's own finally.
    /// </summary>
    [Fact]
    public async Task AbnormalExitWithoutMaxSessionDuration_DoesNotCloseClientWebSocket()
    {
        var options = CreateDefaultOptions(o => o.MaxSessionDuration = null);

        var sessionTask = await StartSessionInBackgroundAsync(options);
        FakeWs.EnqueueError(new WebSocketException("simulated transport failure"));

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        FakeWs.State.ShouldBe(WebSocketState.Open);
    }
}
