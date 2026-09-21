using NSubstitute;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using Shouldly;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// One throwing tool handler used to take the whole turn down with it.
///
/// <para>Replies were accumulated across the batch and only sent after the loop, so a handler that
/// threw discarded every sibling's reply as well. Worse, the exception escaped past the turn
/// completion that follows in the same switch arm — so the turn never completed, the idle timer was
/// never armed, and the call sat there with nothing to move it forward. The only trace was one line
/// from the router's catch-all.</para>
///
/// <para>This chain has seventeen tool handlers doing POS lookups and HTTP calls; treating a batch as
/// all-or-nothing means the most failure-prone step in the system can wedge a live call.</para>
/// </summary>
public class RealtimeAiServiceFunctionCallIsolationTests : RealtimeAiServiceTestBase
{
    private static RealtimeAiWssFunctionCallData Call(string name) => new() { FunctionName = name, CallId = name };

    private void ProviderSuggests(params string[] names) =>
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.FunctionCallSuggested,
            Data = names.Select(Call).ToList()
        });

    private RealtimeSessionOptions FailingHandlerOptions(List<string> ran = null) =>
        CreateDefaultOptions(o => o.OnFunctionCallAsync = (data, _) =>
        {
            if (ran != null) lock (ran) ran.Add(data.FunctionName);
            throw new InvalidOperationException("simulated POS timeout");
        });

    private static int RepliesIn(IEnumerable<string> sent) => sent.Count(m => m.StartsWith("fc_reply:"));

    [Fact]
    public async Task AReplySendFailure_ShouldNotSkipTheRemainingHandlers()
    {
        // The other half of this class's contract, and it was never closed. A failing HANDLER cannot
        // take its siblings down — but a failing SEND could, because the send was outside every catch.
        // On a real call the sibling skipped that way is hangup or transfer_call.
        ProviderSuggests("repeat_order", "hangup");
        FakeWssClient.ThrowOnSend = m => m == "fc_reply:ok:repeat_order";

        var ran = new List<string>();
        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = (data, _) =>
        {
            lock (ran) ran.Add(data.FunctionName);
            return Task.FromResult(new RealtimeAiFunctionCallResult { Output = $"ok:{data.FunctionName}" });
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        ran.ShouldContain("hangup", "the hangup handler must still run when an earlier tool's reply could not be sent");
    }

    [Fact]
    public async Task AFailingTool_ShouldTellTheModelItFailed()
    {
        // Without a reply the model is left with a tool call nobody answered, and it will happily tell
        // the customer an outcome it never received — an order status, a price, a pickup time.
        ProviderSuggests("check_order_status");

        var sessionTask = await StartSessionInBackgroundAsync(FailingHandlerOptions());
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var sent = FakeWssClient.SentMessages.ToList();

        RepliesIn(sent).ShouldBe(1);
        sent.ShouldContain("response_create_msg", "answering without asking the model to speak leaves the caller in the same silence");
    }

    [Fact]
    public async Task TheSameToolFailingAgain_ShouldNotBeAnsweredTwice()
    {
        // THE bound. Answering every time keeps completing turns, and starting the idle timer stops it
        // first — so the 60-second countdown restarts from zero on each one and never reaches term. The
        // phone path has no session ceiling behind it, so that is a call that never ends.
        ProviderSuggests("check_order_status");

        var sessionTask = await StartSessionInBackgroundAsync(FailingHandlerOptions());

        for (var turn = 0; turn < 3; turn++)
        {
            await FakeWssClient.SimulateMessageReceivedAsync("fc");
            await Task.Delay(30);
        }

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        RepliesIn(FakeWssClient.SentMessages).ShouldBe(1, "one recovery attempt, then today's behaviour and today's hangup");
    }

    [Fact]
    public async Task ManyDifferentToolsFailing_ShouldStopAtTheSessionCeiling()
    {
        // Per-tool alone is not a bound: seventeen tools are reachable, so seventeen distinct failures
        // would still be seventeen extra turns. The session ceiling is what makes the worst case small.
        ProviderSuggests("t1", "t2", "t3", "t4", "t5", "t6", "t7", "t8");

        var sessionTask = await StartSessionInBackgroundAsync(FailingHandlerOptions());
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(80);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        RepliesIn(FakeWssClient.SentMessages).ShouldBe(RealtimeAiFunctionCallReplyDefaults.MaxFailureRepliesPerSession);
    }

    [Fact]
    public async Task AFailingToolWithNoCallId_ShouldStaySilent()
    {
        // A reply needs an id to address. Sending one anyway is rejected by the provider, and on a
        // socket that has already closed that rejection is classified critical and drops the call.
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.FunctionCallSuggested,
            Data = new List<RealtimeAiWssFunctionCallData> { new() { FunctionName = "check_order_status" } }
        });

        var sessionTask = await StartSessionInBackgroundAsync(FailingHandlerOptions());
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        RepliesIn(FakeWssClient.SentMessages).ShouldBe(0);
    }

    [Fact]
    public void TheSessionCeiling_ShouldStaySmall()
    {
        // Pinned as the safety property it is. Its whole job is to keep the worst case — every tool
        // failing on a call with no session ceiling — down to a handful of extra turns.
        RealtimeAiFunctionCallReplyDefaults.MaxFailureRepliesPerSession.ShouldBe(3);
    }

    [Fact]
    public async Task OneHandlerThrows_TheOtherRepliesStillReachTheProvider()
    {
        ProviderSuggests("first", "explodes", "third");

        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = (data, _) =>
            data.FunctionName == "explodes"
                ? throw new InvalidOperationException("simulated POS timeout")
                : Task.FromResult(new RealtimeAiFunctionCallResult { Output = $"ok:{data.FunctionName}" }));

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("fc");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var sent = FakeWssClient.SentMessages.ToList();

        sent.ShouldContain("fc_reply:ok:first");
        sent.ShouldContain("fc_reply:ok:third");
    }

    [Fact]
    public async Task HandlerThrows_TheTurnStillCompletes()
    {
        // The consequence that actually strands a caller: no turn completion means no idle timer and
        // nothing to move the conversation on.
        ProviderSuggests("explodes");

        var options = CreateDefaultOptions(o =>
        {
            o.IdleFollowUp = new RealtimeSessionIdleFollowUp { TimeoutSeconds = 30, FollowUpMessage = "still there?" };
            o.OnFunctionCallAsync = (_, _) => throw new InvalidOperationException("simulated handler failure");
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        ClientAdapter.Received().BuildTurnCompletedMessage(Arg.Any<string>());
        TimerManager.Received().StartTimer(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task EveryHandlerThrows_TheTurnStillCompletes()
    {
        ProviderSuggests("a", "b");

        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = (_, _) => throw new InvalidOperationException("all down"));

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("fc");
        await Task.Delay(50);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        ClientAdapter.Received().BuildTurnCompletedMessage(Arg.Any<string>());
    }

    [Fact]
    public async Task HandlerFailure_ShouldBeReportedAgainstTheToolThatFailed()
    {
        // "a function call failed" is not actionable across seventeen tools.
        using var context = Serilog.Sinks.TestCorrelator.TestCorrelator.CreateContext();
        ProviderSuggests("explodes");

        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = (_, _) => throw new InvalidOperationException("boom"));

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("fc");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Serilog.Sinks.TestCorrelator.TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.MessageTemplate.Text.Contains("Function call failed"))
            .Properties["FunctionName"].ToString().ShouldContain("explodes");
    }

    [Fact]
    public async Task AllHandlersSucceed_BehaviourIsUnchanged()
    {
        // The healthy path is the one that must stay byte-identical.
        ProviderSuggests("first", "second");

        var options = CreateDefaultOptions(o => o.OnFunctionCallAsync = (data, _) =>
            Task.FromResult(new RealtimeAiFunctionCallResult { Output = $"ok:{data.FunctionName}" }));

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("fc");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var sent = FakeWssClient.SentMessages.ToList();

        sent.ShouldContain("fc_reply:ok:first");
        sent.ShouldContain("fc_reply:ok:second");
        sent.ShouldContain("response_create_msg");
    }
}
