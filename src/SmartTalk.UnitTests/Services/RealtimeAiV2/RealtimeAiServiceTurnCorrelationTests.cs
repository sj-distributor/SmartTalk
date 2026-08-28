using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The per-turn measurements this branch added — first-audio latency, turn duration, token usage —
/// were not joinable to each other. Round is incremented inside the turn-completion handler BEFORE its
/// own log line, while every other per-turn line reads the pre-increment value, so an operator
/// faceting on Round joined the start of turn N to the completion of turn N-1. And the token
/// breakdown was logged twice under identical property names, once by the engine and once by the
/// phone consumer, so any Seq sum was exactly double with nothing in the data to show it.
///
/// <para>"Which turn was slow, and what did it cost" is the question those lines exist to answer.</para>
/// </summary>
public class RealtimeAiServiceTurnCorrelationTests : RealtimeAiServiceTestBase
{
    private void ProviderRunsATurnWithUsage() =>
        ProviderAdapter.ParseMessage("started").Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted })
            .AndDoes(_ => { });

    private static LogEvent Line(string fragment) =>
        TestCorrelator.GetLogEventsFromCurrentContext().Single(e => e.MessageTemplate.Text.Contains(fragment));

    private static IEnumerable<LogEvent> Lines(string fragment) =>
        TestCorrelator.GetLogEventsFromCurrentContext().Where(e => e.MessageTemplate.Text.Contains(fragment));

    private async Task<Task> RunOneTurnAsync()
    {
        ProviderAdapter.ParseMessage("started").Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted });
        ProviderAdapter.ParseMessage("done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted,
            Usage = new RealtimeAiWssUsageData { TotalTokens = 100, InputTokens = 60, OutputTokens = 40 }
        });

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("done");
        await Task.Delay(50);

        return sessionTask;
    }

    [Fact]
    public async Task ATurnsCompletionLine_ShouldCarryTheSameRoundAsItsStartLine()
    {
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await RunOneTurnAsync();

        var started = Line("Turn started");
        var completed = Line("AI turn completed");

        completed.Properties["Round"].ToString()
            .ShouldBe(started.Properties["Round"].ToString(), "a turn's start and completion must be joinable on Round");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ATurnsTokenUsage_ShouldCarryThatTurnsRound()
    {
        // Usage arrives with the same response.done that completes the turn, so it must file under the
        // turn it belongs to — otherwise "which turn was slow and what did it cost" stays unanswerable.
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await RunOneTurnAsync();

        Line("Token usage reported").Properties["Round"].ToString().ShouldBe(Line("AI turn completed").Properties["Round"].ToString());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TheCompletionLine_ShouldStillCarryTheNumberTheIdleGateReads()
    {
        // Round became the 0-based index of the turn described, but the idle follow-up gate compares
        // SkipRounds against the POST-increment count. That number was only ever visible on this line,
        // and it is what an on-call engineer reads to answer "why didn't the follow-up fire". Kept as
        // its own property rather than lost to the realignment.
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await RunOneTurnAsync();

        var completed = Line("AI turn completed");

        completed.Properties["Round"].ToString().ShouldBe("0");
        completed.Properties["TurnsCompleted"].ToString().ShouldBe("1");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TokenUsage_ShouldBeReportedExactlyOncePerTurn()
    {
        // Logged by both the engine and the phone consumer under identical property names, any Seq sum
        // over Total came back at exactly twice the real token count, and neither line could simply be
        // filtered out without losing a dimension the other did not carry.
        using var context = TestCorrelator.CreateContext();

        var sessionTask = await RunOneTurnAsync();

        Lines("Token usage").Count().ShouldBe(1);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
