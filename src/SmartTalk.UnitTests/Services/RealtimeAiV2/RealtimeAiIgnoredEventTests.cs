using Microsoft.Extensions.Configuration;
using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Splits "the provider sent something we deliberately do not act on" from "the provider sent
/// something we do not recognise".
///
/// <para>Both used to land on <c>Unknown</c>, which the engine logs at Warning together with the
/// entire raw frame. OpenAI GA emits several such events per conversational round trip, so a 15-turn
/// order call produced 90-135 Warnings — most describing entirely normal behaviour, and some carrying
/// the function-call arguments, i.e. the customer's order, name and phone. That simultaneously buried
/// real warnings, dominated Seq volume, and wrote customer data into the sink at high frequency.</para>
///
/// <para>The engine's behaviour is identical for both: neither triggers a handler. That equivalence
/// is what makes this a logging change rather than a behavioural one, and
/// <see cref="IgnoredEvent_ShouldNotTriggerAnyHandler"/> is its machine-checkable proof.</para>
/// </summary>
public class RealtimeAiIgnoredEventTests : RealtimeAiServiceTestBase
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() => new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    [Theory]
    [InlineData("session.created")]
    [InlineData("conversation.item.created")]
    [InlineData("conversation.item.added")]
    [InlineData("response.output_item.added")]
    [InlineData("input_audio_buffer.committed")]
    [InlineData("input_audio_buffer.speech_stopped")]
    [InlineData("rate_limits.updated")]
    public void KnownBenignProviderEvent_ShouldParseAsIgnoredNotUnknown(string eventType)
    {
        NewAdapter().ParseMessage($$"""{"type":"{{eventType}}"}""")
            .Type.ShouldBe(RealtimeAiWssEventType.Ignored);
    }

    [Fact]
    public void GenuinelyUnrecognisedEvent_ShouldStillParseAsUnknown()
    {
        NewAdapter().ParseMessage("""{"type":"something.nobody.has.seen"}""")
            .Type.ShouldBe(RealtimeAiWssEventType.Unknown);
    }

    [Fact]
    public async Task IgnoredEvent_ShouldNotTriggerAnyHandler()
    {
        // The equivalence that makes this non-behavioural: Ignored does exactly what Unknown did —
        // nothing. If a future edit routed an Ignored event into a handler, this fails.
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Ignored, RawJson = "{}" });

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("benign");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        ClientAdapter.DidNotReceive().BuildTurnCompletedMessage(Arg.Any<string>());
        ClientAdapter.DidNotReceive().BuildErrorMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        ClientAdapter.DidNotReceive().BuildAudioDeltaMessage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task IgnoredEvent_ShouldNotEmitAWarning()
    {
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Ignored, RawJson = "{}" });

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("benign");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Level >= LogEventLevel.Warning)
            .ShouldBeEmpty("normal provider chatter must not consume the level on-call alerts on");
    }

    [Fact]
    public async Task UnknownEvent_ShouldWarnWithoutReproducingTheFrame()
    {
        // The raw frame is where the order arguments live. The event type is the diagnostic part.
        using var context = TestCorrelator.CreateContext();

        ProviderAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.Unknown,
            Data = "mystery.event",
            RawJson = """{"arguments":"SENTINEL-ORDER-PAYLOAD"}"""
        });

        var sessionTask = await StartSessionInBackgroundAsync();
        await FakeWssClient.SimulateMessageReceivedAsync("mystery");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var warning = TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("Unknown provider event"));

        warning.RenderMessage().ShouldNotContain("SENTINEL-ORDER-PAYLOAD");
        warning.Properties["ProviderEventType"].ToString().ShouldContain("mystery.event");
    }
}
