using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.Twilio;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Twilio's media-streams protocol requires every outbound `media` / `clear` / `mark`
/// frame to carry the exact <c>streamSid</c> Twilio gave us in its <c>start</c> event.
/// Sending one of these frames before <c>start</c> arrives — or with a fabricated
/// <c>streamSid</c> — causes Twilio to silently drop the frame.
///
/// <para>
/// Pre-fix the adapter used <c>_streamSid ?? sessionId</c>, so during the race window
/// (OpenAI <c>session.updated</c> arriving before Twilio's <c>start</c>) the greeting
/// audio was sent with our internal session GUID as <c>streamSid</c> and Twilio
/// dropped it. Caller heard silence at the start of the call.
/// </para>
///
/// <para>
/// Post-fix the adapter returns <c>null</c> when <c>_streamSid</c> isn't yet set;
/// the orchestrator's <c>SendToClientAsync</c> skips null payloads. The caller still
/// hears silence during the race window (we can't fabricate a valid streamSid), but
/// (a) we no longer waste bandwidth on garbage frames, and (b) we get a Serilog
/// warning naming the race so we can measure how often it happens.
/// </para>
/// </summary>
public class TwilioStreamSidGuardTests
{
    private const string SessionIdFallback = "00000000-0000-0000-0000-000000000001";

    private static TwilioRealtimeAiClientAdapter NewAdapter() => new();

    private static void SimulateTwilioStart(TwilioRealtimeAiClientAdapter adapter, string streamSid)
    {
        var startMessage = "{\"event\":\"start\",\"start\":{\"streamSid\":\"" + streamSid + "\",\"callSid\":\"CA-test\"}}";
        adapter.ParseMessage(startMessage);
    }

    [Fact]
    public void BuildAudioDeltaMessage_BeforeStart_ReturnsNull()
    {
        var adapter = NewAdapter();

        var message = adapter.BuildAudioDeltaMessage("base64", SessionIdFallback);

        message.ShouldBeNull();
    }

    [Fact]
    public void BuildAudioDeltaMessage_AfterStart_ReturnsMessageWithRealStreamSid()
    {
        var adapter = NewAdapter();
        SimulateTwilioStart(adapter, "MZ-real-stream-sid");

        var message = adapter.BuildAudioDeltaMessage("base64", SessionIdFallback);

        message.ShouldNotBeNull();
        // Reflect on the anonymous shape to assert streamSid: { @event = "media", streamSid, media = { payload } }
        var streamSid = message.GetType().GetProperty("streamSid")!.GetValue(message)!.ToString();
        streamSid.ShouldBe("MZ-real-stream-sid");
    }

    [Fact]
    public void BuildSpeechDetectedMessage_BeforeStart_ReturnsNull()
    {
        var adapter = NewAdapter();

        adapter.BuildSpeechDetectedMessage(SessionIdFallback).ShouldBeNull();
    }

    [Fact]
    public void BuildSpeechDetectedMessage_AfterStart_ReturnsMessageWithRealStreamSid()
    {
        var adapter = NewAdapter();
        SimulateTwilioStart(adapter, "MZ-stream-2");

        var message = adapter.BuildSpeechDetectedMessage(SessionIdFallback);

        message.ShouldNotBeNull();
        message.GetType().GetProperty("streamSid")!.GetValue(message)!.ToString().ShouldBe("MZ-stream-2");
    }

    [Fact]
    public void BuildTurnCompletedMessage_BeforeStart_ReturnsNull()
    {
        var adapter = NewAdapter();

        adapter.BuildTurnCompletedMessage(SessionIdFallback).ShouldBeNull();
    }

    [Fact]
    public void BuildTurnCompletedMessage_AfterStart_ReturnsMessageWithRealStreamSid()
    {
        var adapter = NewAdapter();
        SimulateTwilioStart(adapter, "MZ-stream-3");

        var message = adapter.BuildTurnCompletedMessage(SessionIdFallback);

        message.ShouldNotBeNull();
        message.GetType().GetProperty("streamSid")!.GetValue(message)!.ToString().ShouldBe("MZ-stream-3");
    }
}
