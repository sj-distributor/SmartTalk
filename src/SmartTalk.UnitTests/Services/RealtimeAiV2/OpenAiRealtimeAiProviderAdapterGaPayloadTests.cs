using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the OpenAI Realtime API GA contract (post 2026-05-07).
/// Beta-era fields that would now be rejected by the server MUST stay out of the payload.
/// </summary>
public class OpenAiRealtimeAiProviderAdapterGaPayloadTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeSessionOptions OptionsWithPromptAndVoice(string prompt = "you are helpful", string voice = "alloy") =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = prompt,
                Voice = voice,
                Tools = new List<object>()
            }
        };

    private static JObject SerializeSession(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload));

    // ── Headers ────────────────────────────────────────────────────

    [Fact]
    public void GetHeaders_DoesNotIncludeBetaHeader_AfterGaCutover()
    {
        // The `OpenAI-Beta: realtime=v1` header was removed at GA cutover. Sending it now
        // produces `invalid_beta` and the WS is closed before our first session.update.
        var adapter = NewAdapter();

        var headers = adapter.GetHeaders(RealtimeAiServerRegion.US);

        headers.ShouldNotContainKey("OpenAI-Beta");
        headers.ShouldContainKey("Authorization");
    }

    // ── Session payload top-level shape ────────────────────────────

    [Fact]
    public void BuildSessionConfig_TopLevel_HasSessionTypeRealtime()
    {
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(), RealtimeAiAudioCodec.MULAW));

        json["type"]!.Value<string>().ShouldBe("session.update");
        json["session"]!["type"]!.Value<string>().ShouldBe("realtime");
    }

    [Fact]
    public void BuildSessionConfig_UsesOutputModalities_NotLegacyModalities()
    {
        // GA renamed `modalities` → `output_modalities` and dropped the input direction
        // (it is always inferred from session.type).
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(), RealtimeAiAudioCodec.MULAW));

        json["session"]!["modalities"].ShouldBeNull();
        json["session"]!["output_modalities"]!.Values<string>().ShouldBe(new[] { "audio" });
    }

    [Fact]
    public void BuildSessionConfig_DoesNotEmitTemperature_AfterGaCutover()
    {
        // `temperature` was removed from the GA session payload schema.
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(), RealtimeAiAudioCodec.MULAW));

        json["session"]!["temperature"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_DoesNotEmitFlatAudioFields_AfterGaCutover()
    {
        // GA moved audio config under `session.audio.{input,output}` and removed the flat
        // `input_audio_format` / `output_audio_format` / `voice` / `input_audio_transcription`
        // siblings on `session`.
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(), RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["input_audio_format"].ShouldBeNull();
        session["output_audio_format"].ShouldBeNull();
        session["input_audio_transcription"].ShouldBeNull();
        session["voice"].ShouldBeNull();
        session["turn_detection"].ShouldBeNull();
    }

    // ── Audio format object (codec mapping) ────────────────────────

    [Theory]
    [InlineData(RealtimeAiAudioCodec.MULAW, "audio/pcmu")]
    [InlineData(RealtimeAiAudioCodec.ALAW, "audio/pcma")]
    public void BuildSessionConfig_G711Codec_FormatHasTypeOnly_NoRateField(RealtimeAiAudioCodec codec, string expectedType)
    {
        // G.711 is fixed at 8 kHz; sending a `rate` field is rejected by the GA server as
        // an unknown property. Canonical Twilio sample emits {"type": "audio/pcmu"} only.
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(), codec));

        var inputFormat = json["session"]!["audio"]!["input"]!["format"]!;
        inputFormat["type"]!.Value<string>().ShouldBe(expectedType);
        inputFormat["rate"].ShouldBeNull();

        var outputFormat = json["session"]!["audio"]!["output"]!["format"]!;
        outputFormat["type"]!.Value<string>().ShouldBe(expectedType);
        outputFormat["rate"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_Pcm16Codec_FormatCarriesExplicitRate()
    {
        // PCM16 rate is not fixed; the GA contract requires us to declare it explicitly.
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(), RealtimeAiAudioCodec.PCM16));

        var inputFormat = json["session"]!["audio"]!["input"]!["format"]!;
        inputFormat["type"]!.Value<string>().ShouldBe("audio/pcm");
        inputFormat["rate"]!.Value<int>().ShouldBe(24000);

        var outputFormat = json["session"]!["audio"]!["output"]!["format"]!;
        outputFormat["type"]!.Value<string>().ShouldBe("audio/pcm");
        outputFormat["rate"]!.Value<int>().ShouldBe(24000);
    }

    // ── Nested audio config is preserved ────────────────────────────

    [Fact]
    public void BuildSessionConfig_AudioInput_CarriesTranscriptionAndTurnDetection()
    {
        var adapter = NewAdapter();
        var options = OptionsWithPromptAndVoice();
        options.ModelConfig.TurnDetection = new { type = "server_vad" };

        var json = SerializeSession(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        var input = json["session"]!["audio"]!["input"]!;
        input["transcription"]!["model"]!.Value<string>().ShouldBe("whisper-1");
        input["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
    }

    [Fact]
    public void BuildSessionConfig_AudioOutput_CarriesVoice()
    {
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(voice: "shimmer"), RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("shimmer");
    }

    [Fact]
    public void BuildSessionConfig_VoiceFallsBackToAlloy_WhenUnset()
    {
        // Preserves prior behavior: empty/null voice → alloy default.
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(voice: ""), RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("alloy");
    }

    [Fact]
    public void BuildSessionConfig_PreservesInstructionsAtSessionLevel()
    {
        var adapter = NewAdapter();

        var json = SerializeSession(adapter.BuildSessionConfig(OptionsWithPromptAndVoice(prompt: "你是收銀員"), RealtimeAiAudioCodec.MULAW));

        json["session"]!["instructions"]!.Value<string>().ShouldBe("你是收銀員");
    }
}
