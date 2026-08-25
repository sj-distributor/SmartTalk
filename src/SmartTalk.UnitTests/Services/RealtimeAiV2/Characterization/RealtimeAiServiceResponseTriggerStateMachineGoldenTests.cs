using System.Net.WebSockets;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the response-trigger queue/drain/rollback state machine
/// (RealtimeAiService.Send.cs: QueueOrTriggerProviderResponseAsync, MarkProviderResponseStartedAsync,
/// MarkProviderResponseCompletedAndDrainAsync, and the send-failure catch-block rollback).
///
/// This machine has no direct test today. It is driven here through the public surface: the
/// session action SendTextToProviderAsync queues/triggers, a ResponseStarted event marks
/// in-progress, and a ResponseTurnCompleted event drains. The observable is the number of
/// BuildTriggerResponseMessage ("response_create_msg") frames sent to the provider.
///
/// Migration step S9 (a single _turnLock) is a direct rewrite of this ProviderResponseStateLock
/// machine; the queue / drain-on-completion / rollback semantics must be preserved byte-for-byte.
/// </summary>
public class RealtimeAiServiceResponseTriggerStateMachineGoldenTests : RealtimeAiServiceTestBase
{
    private const string Trigger = "response_create_msg";   // ProviderAdapter.BuildTriggerResponseMessage stub

    private async Task<(Task SessionTask, RealtimeAiSessionActions Actions)> StartAndCaptureActionsAsync()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "init" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized },
            "started" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted },
            "done" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted, Data = new List<RealtimeAiWssFunctionCallData>() },
            _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown }
        });

        RealtimeAiSessionActions? actions = null;
        var options = CreateDefaultOptions(o => o.OnSessionReadyAsync = a => { actions = a; return Task.CompletedTask; });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("init");   // → OnSessionReadyAsync captures actions (inline-awaited)
        actions.ShouldNotBeNull();

        return (sessionTask, actions!);
    }

    private int TriggerCount() => FakeWssClient.SentMessages.Count(m => m == Trigger);

    [Fact]
    public async Task SendText_WhenNoResponseInProgress_TriggersImmediately()
    {
        var (sessionTask, actions) = await StartAndCaptureActionsAsync();

        await actions.SendTextToProviderAsync("hi");

        TriggerCount().ShouldBe(1);   // not in progress → trigger fires immediately

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task TwoTriggersWhileInProgress_QueueOnce_ThenDrainSendsExactlyOneOnTurnComplete()
    {
        var (sessionTask, actions) = await StartAndCaptureActionsAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("started");   // MarkProviderResponseStarted → in-progress

        await actions.SendTextToProviderAsync("a");                    // in-progress → queued, no trigger
        await actions.SendTextToProviderAsync("b");                    // in-progress → still queued, no trigger

        TriggerCount().ShouldBe(0);

        await FakeWssClient.SimulateMessageReceivedAsync("done");      // turn complete → drain queued retry

        TriggerCount().ShouldBe(1);   // two queued triggers collapse into exactly one drained send

        FakeWs.EnqueueClose();
        await sessionTask;
    }

    [Fact]
    public async Task TriggerSendFailure_RollsBackPendingFlag_RetryDrainsOnNextTurnComplete()
    {
        var (sessionTask, actions) = await StartAndCaptureActionsAsync();

        var threwOnce = false;
        FakeWssClient.ThrowOnSend = msg =>
        {
            if (!threwOnce && msg == Trigger) { threwOnce = true; return true; }
            return false;
        };

        // Not in progress → first SendText tries to trigger → send throws → catch rolls back
        // (IsProviderResponseInProgress=false, HasPendingProviderResponseTrigger=true) and rethrows.
        await Should.ThrowAsync<WebSocketException>(() => actions.SendTextToProviderAsync("a"));

        TriggerCount().ShouldBe(0);   // the failed trigger was not recorded

        // A turn-complete drains the rolled-back-and-requeued retry → the trigger is re-sent ONCE
        // (now succeeds), proving the rollback restored the pending flag rather than dropping it.
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        TriggerCount().ShouldBe(1);

        FakeWs.EnqueueClose();
        await sessionTask;
    }
}
