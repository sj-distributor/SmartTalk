using NSubstitute;
using SmartTalk.Core.Services.RealtimeAiV2;
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
