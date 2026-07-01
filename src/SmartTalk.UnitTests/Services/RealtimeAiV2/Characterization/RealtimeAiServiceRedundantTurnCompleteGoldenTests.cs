using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins how the BuiltIn turn-completion responds to provider response.done
/// events. A response.done preceded by its own ResponseStarted completes the turn once; the CURRENT
/// behavior for a redundant response.done with NO intervening ResponseStarted is captured exactly (so
/// S9-S12's turn-stamped gate either preserves it or makes any de-dup change a deliberate RED-then-GREEN).
/// </summary>
public class RealtimeAiServiceRedundantTurnCompleteGoldenTests : RealtimeAiServiceTestBase
{
    private int TurnCompletedCount() => FakeWs.GetSentTextMessages().Count(m => m.Contains("AiTurnCompleted"));

    private void StubEvents() =>
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "started" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted },
            _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted, Data = new List<RealtimeAiWssFunctionCallData>() }
        });

    [Fact]
    public async Task TwoNormalTurns_EachWithResponseStarted_CompleteTwice()
    {
        StubEvents();
        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("done");
        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        FakeWs.EnqueueClose();
        await sessionTask;

        TurnCompletedCount().ShouldBe(2);
    }

    [Fact]
    public async Task TwoResponseDone_NoResponseStartedBetween_BuiltInCompletesPerDone()
    {
        StubEvents();
        var sessionTask = await StartSessionInBackgroundAsync();

        // Two response.done with NO intervening ResponseStarted/ResetCurrentResponseState. In BuiltIn
        // mode TryMarkCurrentResponseTurnCompletedLocked returns true on each (it short-circuits before
        // the CurrentResponseTurnCompletedHandled de-dup, which only applies to external TTS), so both
        // complete. Pinned as the current ground truth.
        await FakeWssClient.SimulateMessageReceivedAsync("done");
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        FakeWs.EnqueueClose();
        await sessionTask;

        TurnCompletedCount().ShouldBe(2);
    }
}
