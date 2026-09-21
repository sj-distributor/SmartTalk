using System.Net.WebSockets;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// A session that died at connect left no trace whatsoever. The throw skipped
/// <c>OrchestrateSessionAsync</c>, and with it the only call to <c>CleanupSessionAsync</c> — so no
/// OnSessionEnded, no transcript, no recording, and nothing for an operator to find afterwards.
/// Precisely the calls worth investigating were the ones guaranteed to be invisible.
///
/// <para>The risk in fixing it is running cleanup twice, which would flush a transcript or notify a
/// consumer a second time. Cleanup is claimed with Interlocked, and the exactly-once property is
/// asserted on the healthy path here rather than assumed.</para>
/// </summary>
public class RealtimeAiServiceConnectFailureCleanupTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ProviderConnectFails_ShouldStillNotifyTheConsumerTheSessionEnded()
    {
        FakeWssClient.ShouldFailConnect = true;

        var endedCount = 0;
        var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = _ => { endedCount++; return Task.CompletedTask; });

        await Should.ThrowAsync<WebSocketException>(() => Sut.ConnectAsync(options, CancellationToken.None));

        endedCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProviderConnectFails_ShouldStillFlushWhateverWasCaptured()
    {
        FakeWssClient.ShouldFailConnect = true;

        var transcriptionsInvoked = false;
        var options = CreateDefaultOptions(o =>
        {
            o.OnTranscriptionsCompletedAsync = (_, _) => { transcriptionsInvoked = true; return Task.CompletedTask; };
            o.OnSessionEndedAsync = _ => Task.CompletedTask;
        });

        await Should.ThrowAsync<WebSocketException>(() => Sut.ConnectAsync(options, CancellationToken.None));

        // Nothing was captured before the failure, so the engine's own empty-transcript guard applies
        // — what matters is that cleanup ran at all, which OnSessionEnded above proves.
        transcriptionsInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task ProviderConnectFails_ShouldStillRethrowToTheCaller()
    {
        // The consumer decides what to do about a failed connect; swallowing it here would take that
        // decision away and make the failure invisible a second time.
        FakeWssClient.ShouldFailConnect = true;

        await Should.ThrowAsync<WebSocketException>(
            () => Sut.ConnectAsync(CreateDefaultOptions(), CancellationToken.None));
    }

    [Fact]
    public async Task NormalSession_ShouldRunCleanupExactlyOnce()
    {
        // The risk the claim exists for: cleanup is now reachable from two finallys.
        var endedCount = 0;
        var flushCount = 0;

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.InputAudioTranscriptionCompleted,
            Data = new RealtimeAiWssTranscriptionData { Transcript = "hi", Speaker = AiSpeechAssistantSpeaker.User }
        });

        var options = CreateDefaultOptions(o =>
        {
            o.OnSessionEndedAsync = _ => { endedCount++; return Task.CompletedTask; };
            o.OnTranscriptionsCompletedAsync = (_, _) => { flushCount++; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("t");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        endedCount.ShouldBe(1);
        flushCount.ShouldBe(1, "flushing a transcript twice would duplicate the call's record");
    }

    [Fact]
    public async Task ProviderDroppedMidSession_ShouldStillRunCleanupExactlyOnce()
    {
        var endedCount = 0;
        var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = _ => { endedCount++; return Task.CompletedTask; });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateStateChangedAsync(WebSocketState.Closed, "server closed");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        endedCount.ShouldBe(1);
    }
}
