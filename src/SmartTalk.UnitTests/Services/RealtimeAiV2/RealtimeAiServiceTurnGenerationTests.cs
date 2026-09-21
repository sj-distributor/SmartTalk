using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The turn gate waits on two legs in external-TTS mode: the provider finishing its response, and
/// the synthesizer finishing its audio. The synthesizer's signal arrives from its own socket and
/// says nothing about which turn it belongs to.
///
/// <para>Both watchdogs already compare a generation stamp before touching the gate. The real
/// completion path did not — it was the one gate entrant with no such check. So a synthesis signal
/// for a turn that had already been superseded would satisfy the NEXT turn's gate and complete it
/// while the AI was still speaking: the caller gets cut off mid-sentence, and the Round counter that
/// drives idle follow-up is off by one for the rest of the call.</para>
///
/// <para>The bump and the flag reset also have to be one atomic step. Bumping first and clearing
/// afterwards leaves a window where a stamp taken from the new generation lands against the old
/// turn's flags.</para>
/// </summary>
public class RealtimeAiServiceTurnGenerationTests : RealtimeAiServiceTestBase
{
    private readonly FakeTextSynthesizerTtsProvider _tts = new();

    private RealtimeSessionOptions ExternalTtsSession()
    {
        Switcher.TtsProvider(Arg.Any<RealtimeAiTtsProviderType>()).Returns(_tts);

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) switch
        {
            "started" => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted },
            "text" => new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTextDelta,
                Data = new RealtimeAiWssTextData { Text = "hello" }
            },
            _ => new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted }
        });

        return CreateDefaultOptions(o => o.TtsConfig = new RealtimeAiTtsConfig
        {
            ProviderType = RealtimeAiTtsProviderType.MiniMax,
            TargetCodec = RealtimeAiAudioCodec.PCM16
        });
    }

    /// <summary>
    /// Documents a limitation the engine cannot close on its own, proven rather than assumed.
    ///
    /// <para>When turn one is still waiting on synthesis as turn two starts, turn one's late signal
    /// satisfies turn two's gate and completes it while the AI is still speaking. Stamping the gate
    /// with a turn generation does not help: turn two hands the synthesizer its own text before the
    /// stale signal lands, so any stamp the engine can take already reads as current. The two
    /// signals are genuinely indistinguishable at this boundary — one provider object, an event that
    /// carries no turn identity.</para>
    ///
    /// <para>Closing it requires the TTS contract to carry the turn the signal belongs to, which is
    /// an interface change and belongs with the turn-state restructuring rather than here. Until
    /// then the 8-second synthesis watchdog is what bounds the damage. This test asserts the
    /// current, wrong behaviour so the fix shows up as a deliberate edit.</para>
    /// </summary>
    [Fact]
    public async Task LateSynthesisSignalFromASupersededTurn_CurrentlyCompletesTheNewTurnEarly()
    {
        var sessionTask = await StartSessionInBackgroundAsync(ExternalTtsSession());

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("text");
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        ClientAdapter.DidNotReceive().BuildTurnCompletedMessage(Arg.Any<string>());

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("text");

        await _tts.SimulateSynthesisCompletedAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("done");
        await Task.Delay(50);

        ClientAdapter.Received(1).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SynthesisSignalForTheCurrentTurn_ShouldStillCompleteIt()
    {
        // The path that must keep working — the stamp must not make the gate unsatisfiable.
        var sessionTask = await StartSessionInBackgroundAsync(ExternalTtsSession());

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("text");
        await FakeWssClient.SimulateMessageReceivedAsync("done");

        await _tts.SimulateSynthesisCompletedAsync();
        await Task.Delay(50);

        ClientAdapter.Received(1).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SuccessiveTurns_ShouldEachCompleteExactlyOnce()
    {
        var sessionTask = await StartSessionInBackgroundAsync(ExternalTtsSession());

        for (var turn = 0; turn < 3; turn++)
        {
            await FakeWssClient.SimulateMessageReceivedAsync("started");
            await FakeWssClient.SimulateMessageReceivedAsync("text");
            await FakeWssClient.SimulateMessageReceivedAsync("done");
            await _tts.SimulateSynthesisCompletedAsync();
            await Task.Delay(20);
        }

        ClientAdapter.Received(3).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AudioModeTurns_ShouldBeUnaffected()
    {
        // The production path. It never waits on a synthesis leg, so the stamp must not reach it.
        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0) == "started"
            ? new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseStarted }
            : new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted });

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("started");
        await FakeWssClient.SimulateMessageReceivedAsync("done");
        await Task.Delay(50);

        ClientAdapter.Received(1).BuildTurnCompletedMessage(Arg.Any<string>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
