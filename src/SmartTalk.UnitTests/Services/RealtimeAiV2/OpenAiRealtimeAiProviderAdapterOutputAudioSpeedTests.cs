using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Per-assistant output audio speed contract tests for
/// <see cref="OpenAiRealtimeAiProviderAdapter.BuildSessionConfig"/>.
///
/// <para>
/// The load-bearing invariant: when <see cref="RealtimeAiModelConfig.OutputAudioSpeed"/>
/// is null (the realistic state for every assistant with no speed row), the
/// <c>audio.output</c> object MUST NOT contain a <c>speed</c> property — so
/// OpenAI uses its default of 1.0.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterOutputAudioSpeedTests
{
    private static readonly JsonSerializerSettings ProductionSerializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(OpenAiRealtimeAiProviderAdapterTestSettings.BuildConfiguration()));

    private static RealtimeSessionOptions OptionsWithSpeed(decimal? speed) =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                OutputAudioSpeed = speed
            }
        };

    private static JObject SerializeAsProduction(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload, ProductionSerializer));

    // ── Default path: null speed → field absent from payload ──────────────

    [Fact]
    public void BuildSessionConfig_OutputAudioSpeedNull_FieldAbsentFromPayload()
    {
        // Load-bearing: every existing prod assistant has no OutputAudioSpeed row →
        // ModelConfig.OutputAudioSpeed is null → NullValueHandling.Ignore strips the
        // key from `audio.output`. OpenAI uses 1.0 as before.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithSpeed(null), RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["speed"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_OutputAudioSpeedNull_AdjacentOutputFieldsUnchanged()
    {
        // Null speed must not perturb voice or format inside audio.output.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithSpeed(null), RealtimeAiAudioCodec.MULAW));

        var output = json["session"]!["audio"]!["output"]!;
        output["voice"]!.Value<string>().ShouldBe("alloy");
        output["format"]!["type"]!.Value<string>().ShouldBe("audio/pcmu");
        output["speed"].ShouldBeNull();
    }

    // ── Active path: speed set → emitted inside audio.output ──────────────

    [Theory]
    [InlineData("0.25")]   // documented minimum
    [InlineData("0.85")]   // slower-than-natural
    [InlineData("0.9")]    // elderly-customer recommendation
    [InlineData("1.0")]    // explicit natural speed
    [InlineData("1.1")]    // fast-paced
    [InlineData("1.5")]    // documented maximum
    public void BuildSessionConfig_OutputAudioSpeedSet_EmitsSpeedInsideAudioOutput(string speedString)
    {
        var speed = decimal.Parse(speedString, System.Globalization.CultureInfo.InvariantCulture);

        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithSpeed(speed), RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["speed"]!.Value<decimal>().ShouldBe(speed);
    }

    [Fact]
    public void BuildSessionConfig_OutputAudioSpeedSet_DoesNotPerturbInputFields()
    {
        // Activating output speed must not silently shift any input-side field
        // (transcription / turn_detection / noise_reduction / format).
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithSpeed(0.9m), RealtimeAiAudioCodec.MULAW));

        var input = json["session"]!["audio"]!["input"]!;
        input["transcription"]!["model"]!.Value<string>()
            .ShouldBe(OpenAiRealtimeAiProviderAdapter.DefaultTranscriptionModel);
        input["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        input["noise_reduction"].ShouldBeNull();
        input["format"]!["type"]!.Value<string>().ShouldBe("audio/pcmu");
    }

    // ── Coexistence with other per-assistant configs ───────────────────────

    [Fact]
    public void BuildSessionConfig_SpeedAndLanguageAndCap_AllActivateIndependently()
    {
        // Each per-assistant config is an independent dimension. Pin the
        // combined shape so a future PR that touches one cannot silently break
        // the others.
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                TranscriptionLanguage = "yue",
                TranscriptionModel = "gpt-4o-mini-transcribe",
                MaxResponseOutputTokens = 1200,
                OutputAudioSpeed = 0.9m
            }
        };

        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("gpt-4o-mini-transcribe");
        session["audio"]!["input"]!["transcription"]!["language"]!.Value<string>().ShouldBe("yue");
        session["audio"]!["output"]!["speed"]!.Value<decimal>().ShouldBe(0.9m);
        session["max_response_output_tokens"]!.Value<int>().ShouldBe(1200);
    }
}
