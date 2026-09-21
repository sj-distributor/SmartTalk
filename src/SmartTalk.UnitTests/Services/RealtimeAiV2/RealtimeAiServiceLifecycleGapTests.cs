using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Closes the remaining silences in the session lifecycle.
///
/// <para>"The order never reached the POS" was previously unanswerable from logs: the transcript step
/// returns early on an empty queue and the recording step on an empty buffer, both without a word, so
/// Seq showed a session that started and ended with nothing in between. An operator could not
/// distinguish "nothing was ever transcribed" from "the callback was never wired" from "cleanup threw
/// before reaching that step".</para>
///
/// <para>Transcoding had the same shape: a codec mismatch throws on every single frame and the whole
/// call is garbled, while the only evidence is a generic message-processing error.</para>
/// </summary>
public class RealtimeAiServiceLifecycleGapTests : RealtimeAiServiceTestBase
{
    private static IEnumerable<LogEvent> Captured() => TestCorrelator.GetLogEventsFromCurrentContext();

    [Fact]
    public async Task TurnStart_ShouldBeLogged()
    {
        // Without this the only per-turn line is the completion, so a turn that never finishes leaves
        // no trace that it began.
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted });

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("started");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var started = Captured().Single(e => e.MessageTemplate.Text.Contains("Turn started"));

        started.Properties["Round"].ToString().ShouldBe("0");
        started.Properties.ContainsKey("OutputMode").ShouldBeTrue();
    }

    [Fact]
    public async Task CompletedCallWithNoTranscript_ShouldWarn()
    {
        // A finished call that transcribed nothing is always worth a look — it is exactly the shape of
        // the calls that silently never reach the database.
        using var context = TestCorrelator.CreateContext();

        var options = CreateDefaultOptions(o => o.OnTranscriptionsCompletedAsync = (_, _) => Task.CompletedTask);
        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Captured().ShouldContain(e =>
            e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("no transcriptions"));
    }

    [Fact]
    public async Task RecordingEnabledButNothingCaptured_ShouldWarn()
    {
        using var context = TestCorrelator.CreateContext();

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (_, _) => Task.CompletedTask;
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Captured().ShouldContain(e =>
            e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("no audio was recorded"));
    }

    [Fact]
    public async Task CleanupStepFailure_ShouldNameTheStepAsAFacetableValue()
    {
        using var context = TestCorrelator.CreateContext();

        var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = _ => throw new InvalidOperationException("boom"));
        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Captured()
            .Single(e => e.Level == LogEventLevel.Error && e.MessageTemplate.Text.Contains("Cleanup step failed"))
            .Properties["CleanupStep"].ToString()
            .ShouldContain(nameof(RealtimeAiCleanupStep.InvokeSessionEnded));
    }

    [Fact]
    public async Task TranscodeFailure_ShouldBeReportedAsSuchRatherThanAsAGenericError()
    {
        // A codec mismatch throws on every frame and garbles the whole call; the operator needs to see
        // that it is transcoding, not just that something failed while handling a message.
        using var context = TestCorrelator.CreateContext();

        ClientAdapter.NativeAudioCodec.Returns(RealtimeAiAudioCodec.MULAW);
        ProviderAdapter.GetPreferredCodec(Arg.Any<RealtimeAiAudioCodec>()).Returns(RealtimeAiAudioCodec.PCM16);
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = "!!!not-base64!!!" });

        var sessionTask = await StartSessionInBackgroundAsync();
        FakeWs.EnqueueClientMessage("frame");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Captured().ShouldContain(e =>
            e.Level == LogEventLevel.Error && e.MessageTemplate.Text.Contains("Audio transcode failed"));
    }
}
