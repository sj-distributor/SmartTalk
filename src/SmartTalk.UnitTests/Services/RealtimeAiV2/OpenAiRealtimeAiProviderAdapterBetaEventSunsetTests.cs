using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Locks in the post-sunset behaviour: the four Beta-era event names (the ones that
/// hotfix #934 accepted alongside their GA replacements) now map to
/// <see cref="RealtimeAiWssEventType.Unknown"/>. The Unknown classification triggers
/// a Serilog warning at the service layer, giving operators an immediate signal if
/// OpenAI ever regresses and re-emits a Beta-named event in production.
///
/// <para>
/// Sunset rationale: OpenAI completed the GA cutover on 2026-05-07; hotfix #934
/// deployed 2026-05-12; this PR is gated on ≥ 2 weeks of clean production logs
/// (≥ 2026-05-26). Until that observation window passes, this PR stays in draft
/// state — merging early reintroduces risk of silently dropping audio frames if
/// OpenAI ever rolls a region back to Beta names.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterBetaEventSunsetTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(OpenAiRealtimeAiProviderAdapterTestSettings.BuildConfiguration()));

    private static string SerializeEvent(string type, object extras = null)
    {
        if (extras == null) return JsonConvert.SerializeObject(new { type });

        var payload = JObject.FromObject(extras);
        payload["type"] = type;
        return payload.ToString();
    }

    // ── Beta-era names now classified as Unknown ──────────────────────────

    [Theory]
    [InlineData("response.audio.delta")]
    [InlineData("response.audio.done")]
    [InlineData("response.audio_transcript.delta")]
    [InlineData("response.audio_transcript.done")]
    public void ParseMessage_BetaEventName_NowMapsToUnknown(string betaEventName)
    {
        // After sunset, a Beta-named event surfaces as Unknown so the service's
        // existing Log.Warning fires. The audio handler doesn't fire (= silence on
        // the call), but the warning is the contract: regression is loud, not silent.
        var raw = SerializeEvent(betaEventName);

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Unknown);
    }

    // ── GA-named events keep working unchanged ────────────────────────────

    [Fact]
    public void ParseMessage_GaAudioDelta_StillProducesResponseAudioDelta()
    {
        var raw = SerializeEvent("response.output_audio.delta", new { delta = "AQID", item_id = "itm-1" });

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDelta);
    }

    [Fact]
    public void ParseMessage_GaAudioDone_StillProducesResponseAudioDone()
    {
        var raw = SerializeEvent("response.output_audio.done");

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDone);
    }

    [Fact]
    public void ParseMessage_GaTranscriptDelta_StillProducesOutputAudioTranscriptionPartial()
    {
        var raw = SerializeEvent("response.output_audio_transcript.delta", new { delta = "hello" });

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.OutputAudioTranscriptionPartial);
    }

    [Fact]
    public void ParseMessage_GaTranscriptDone_StillProducesOutputAudioTranscriptionCompleted()
    {
        var raw = SerializeEvent("response.output_audio_transcript.done", new { transcript = "hello world" });

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.OutputAudioTranscriptionCompleted);
    }
}
