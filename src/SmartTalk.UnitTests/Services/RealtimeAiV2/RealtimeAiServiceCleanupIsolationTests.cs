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
/// CHARACTERIZATION — every step of <c>CleanupSessionAsync</c> runs inside
/// <c>SafeExecuteAsync</c>, which swallows exceptions so one failing consumer callback cannot
/// take the others down with it (RealtimeAiService.Orchestration.cs:122-147).
///
/// <para>That isolation is load-bearing and was completely unpinned. The consumers do real network
/// work inside these callbacks — AiKid uploads the recording to an attachment service
/// (AiKidRealtimeServiceV2.cs:108-119) — so a timeout there must not cost the call its transcript,
/// which is the step that runs afterwards.</para>
///
/// <para>Pinned now because the hardening plan moves cleanup into ConnectAsync's finally and adds a
/// done-latch. A refactor that merged these steps, or reordered them under one try, would silently
/// couple failures that are independent today.</para>
/// </summary>
public class RealtimeAiServiceCleanupIsolationTests : RealtimeAiServiceTestBase
{
    // 320 bytes of PCM16 — enough to make the recording buffer non-empty so HandleRecordingAsync
    // reaches the consumer callback instead of early-returning on an empty buffer.
    private static readonly string AudioPayload = Convert.ToBase64String(new byte[320]);

    private void StubClientAudioFrames() =>
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = AudioPayload });

    private void StubCompletedTranscript() =>
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(_ => new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.InputAudioTranscriptionCompleted,
            Data = new RealtimeAiWssTranscriptionData { Transcript = "hello", Speaker = AiSpeechAssistantSpeaker.User }
        });

    [Fact]
    public async Task RecordingCallbackThrows_TranscriptionCallbackStillInvoked()
    {
        StubClientAudioFrames();
        StubCompletedTranscript();

        var recordingAttempted = false;
        var transcriptionInvoked = false;

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (_, _) =>
            {
                recordingAttempted = true;
                throw new TimeoutException("simulated attachment upload timeout");
            };
            o.OnTranscriptionsCompletedAsync = (_, _) => { transcriptionInvoked = true; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        FakeWs.EnqueueClientMessage("audio-frame");
        await FakeWssClient.SimulateMessageReceivedAsync("transcript");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        recordingAttempted.ShouldBeTrue("the recording step must actually have run for this test to mean anything");
        transcriptionInvoked.ShouldBeTrue("a failing recording upload must not cost the call its transcript");
    }

    [Fact]
    public async Task SessionEndedCallbackThrows_RecordingAndTranscriptionsStillInvoked()
    {
        StubClientAudioFrames();
        StubCompletedTranscript();

        var recordingInvoked = false;
        var transcriptionInvoked = false;

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnSessionEndedAsync = _ => throw new InvalidOperationException("simulated consumer failure");
            o.OnRecordingCompleteAsync = (_, _) => { recordingInvoked = true; return Task.CompletedTask; };
            o.OnTranscriptionsCompletedAsync = (_, _) => { transcriptionInvoked = true; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        FakeWs.EnqueueClientMessage("audio-frame");
        await FakeWssClient.SimulateMessageReceivedAsync("transcript");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        recordingInvoked.ShouldBeTrue();
        transcriptionInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task TranscriptionCallbackThrows_SessionCompletesWithoutRethrow()
    {
        StubCompletedTranscript();

        var options = CreateDefaultOptions(o =>
            o.OnTranscriptionsCompletedAsync = (_, _) => throw new InvalidOperationException("simulated persistence failure"));

        // Awaits ConnectAsync's own task rather than going through StartSessionInBackgroundAsync:
        // that helper wraps the call in a Task.Run whose catch-all would swallow an escaping
        // exception and make this assertion vacuous.
        var connectTask = Sut.ConnectAsync(options, CancellationToken.None);
        await Task.Delay(100);

        await FakeWssClient.SimulateMessageReceivedAsync("transcript");
        FakeWs.EnqueueClose();

        // The last cleanup step throwing must not surface to the caller — for the phone consumer
        // that would escape into ConnectAsync's catch-all and be logged as an unhandled error.
        await Should.NotThrowAsync(() => connectTask.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ProviderDisconnectThrows_ConsumerCallbacksStillInvoked()
    {
        // DisconnectFromProviderAsync runs before all three consumer callbacks; a transport-level
        // failure while closing the provider socket must not skip them.
        StubCompletedTranscript();
        FakeWssClient.ThrowOnDisconnect = true;

        var sessionEnded = false;
        var transcriptionInvoked = false;

        var options = CreateDefaultOptions(o =>
        {
            o.OnSessionEndedAsync = _ => { sessionEnded = true; return Task.CompletedTask; };
            o.OnTranscriptionsCompletedAsync = (_, _) => { transcriptionInvoked = true; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("transcript");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        FakeWssClient.DisconnectCallCount.ShouldBe(1, "the disconnect step must have run and thrown");
        sessionEnded.ShouldBeTrue();
        transcriptionInvoked.ShouldBeTrue();
    }

    /// <summary>
    /// Pins a gap the hardening plan closes in P2.2: when the provider connect fails,
    /// <c>ConnectAsync</c> rethrows after <c>DisconnectFromProviderAsync</c> and
    /// <c>OrchestrateSessionAsync</c> never runs — so <c>CleanupSessionAsync</c>, and with it every
    /// terminal consumer callback, is skipped entirely (RealtimeAiService.cs:33-43). A session that
    /// dies at connect leaves no trace: no OnSessionEnded, no transcript, no recording.
    ///
    /// <para>Pinned as current behaviour so that P2.2 — which moves cleanup into a finally — shows up
    /// as a deliberate, reviewed change to this test rather than an invisible side effect.</para>
    /// </summary>
    [Fact]
    public async Task ProviderConnectFails_TerminalCallbacksAreSkipped()
    {
        FakeWssClient.ShouldFailConnect = true;

        var sessionEnded = false;
        var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = _ => { sessionEnded = true; return Task.CompletedTask; });

        await Should.ThrowAsync<WebSocketException>(() => Sut.ConnectAsync(options, CancellationToken.None));

        sessionEnded.ShouldBeFalse("current behaviour — P2.2 changes this to true and updates this test");
    }
}
