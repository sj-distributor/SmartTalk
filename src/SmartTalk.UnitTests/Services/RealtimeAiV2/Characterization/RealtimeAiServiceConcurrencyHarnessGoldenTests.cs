using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the engine's CURRENT concurrent outcome under its real two-thread
/// model: the provider receive loop and the client read loop mutate one shared <c>_ctx</c> at the
/// same time. The entire rest of the suite is single-threaded (SimulateMessageReceivedAsync awaits
/// inline; client frames are spaced with Task.Delay), so the actual concurrency S9–S12 rewrite has
/// no RED net.
///
/// This harness fires provider audio+barge-in events from a dedicated task while the read loop pumps
/// timestamped client frames, with no sequencing between them, and asserts the invariants that must
/// survive S9's single _turnLock: the session does not crash or hang, it ends cleanly, and the
/// barge-in truncate never receives a negative elapsed-ms (the Math.Max(0, clock-anchor) clamp holds
/// even under torn reads of the nullable clock/anchor).
/// </summary>
public class RealtimeAiServiceConcurrencyHarnessGoldenTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ConcurrentProviderEventsAndClientFrames_NoCrashOrHang_NoNegativeTruncate_EndsClean()
    {
        const int iterations = 200;
        var negativeTruncateSeen = false;

        ClientAdapter.ParseMessage(Arg.Any<string>()).Returns(ci =>
        {
            long.TryParse(ci.ArgAt<string>(0), out var ts);   // monotonic clock from the frame index
            return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = "AAAA", Timestamp = ts };
        });

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "audio" => new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseAudioDelta,
                Data = new RealtimeAiWssAudioData { ItemId = "item", Base64Payload = "AAAA" }
            },
            "speech" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected },
            _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown }
        });

        ProviderAdapter.BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>()).Returns(ci =>
        {
            if (ci.ArgAt<long>(1) < 0) negativeTruncateSeen = true;
            return "truncate_msg";
        });

        string? endedSessionId = null;
        var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = id => { endedSessionId = id; return Task.CompletedTask; });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // Provider thread: interleave audio (sets item id + anchor) and speech (barge-in → truncate),
        // mutating _ctx concurrently with the client read loop below.
        var providerLoop = Task.Run(async () =>
        {
            for (var i = 0; i < iterations; i++)
            {
                await FakeWssClient.SimulateMessageReceivedAsync("audio");
                await FakeWssClient.SimulateMessageReceivedAsync("speech");
            }
        });

        // Client read loop: pump timestamped frames with no sequencing against the provider thread.
        var clientLoop = Task.Run(() =>
        {
            for (var i = 1; i <= iterations; i++)
                FakeWs.EnqueueClientMessage(i.ToString());
        });

        await Task.WhenAll(providerLoop, clientLoop);
        await Task.Delay(100);   // let the read loop drain queued frames

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(15));   // must not deadlock

        endedSessionId.ShouldNotBeNullOrEmpty();   // session reached clean cleanup
        negativeTruncateSeen.ShouldBeFalse();      // clamp holds under concurrent torn reads
    }
}
