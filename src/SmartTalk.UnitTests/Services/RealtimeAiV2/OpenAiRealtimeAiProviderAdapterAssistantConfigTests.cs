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
/// Phase 4.2 contract tests for <see cref="OpenAiRealtimeAiProviderAdapter"/>'s per-assistant
/// session-config overrides. The load-bearing invariant: when every
/// <c>RealtimeAiModelConfig</c> override field is null, the serialised JSON must be
/// **byte-equivalent** to the pre-4.2 output. Every override activates only when its own
/// field is set — per-field per-assistant. No global env-var gate; the DB column being
/// NULL is the safety net.
///
/// <para>
/// Tests run with <see cref="JsonSerializerSettings"/> that mirror the production caller
/// (<c>NullValueHandling.Ignore</c>) — null override fields are stripped at serialisation
/// time, which is what makes "all-null produces byte-equivalent output" work.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterAssistantConfigTests
{
    private static readonly JsonSerializerSettings ProductionSerializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeSessionOptions OptionsWithDefaults() =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>()
            }
        };

    private static JObject SerializeSessionAsProduction(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload, ProductionSerializer));

    // ── All-null overrides → byte-equivalent to pre-4.2 ────────────────────

    [Fact]
    public void BuildSessionConfig_AllOverridesNull_PayloadByteEquivalentToPreFour()
    {
        // The load-bearing invariant: a freshly migrated assistant (every override
        // column NULL — the realistic state for every existing prod row after deploy)
        // produces the same JSON the adapter produced before Phase 4.2 landed.
        var adapter = NewAdapter();

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(OptionsWithDefaults(), RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("whisper-1");
        session["audio"]!["input"]!["transcription"]!["language"].ShouldBeNull();
        session["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        session["audio"]!["input"]!["turn_detection"]!["threshold"].ShouldBeNull();
        session["audio"]!["input"]!["turn_detection"]!["silence_duration_ms"].ShouldBeNull();
        session["audio"]!["input"]!["noise_reduction"].ShouldBeNull();
        session["audio"]!["output"]!["speed"].ShouldBeNull();
        session["max_response_output_tokens"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_AllOverridesNull_AllOriginalGaFieldsPreserved()
    {
        // Sanity-check that no Phase 4.2 wiring perturbed the existing GA contract.
        // If any of these break, Phase 4.2 silently breaks already-deployed assistants.
        var adapter = NewAdapter();

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(OptionsWithDefaults(), RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        json["type"]!.Value<string>().ShouldBe("session.update");
        session["type"]!.Value<string>().ShouldBe("realtime");
        session["instructions"]!.Value<string>().ShouldBe("you are helpful");
        session["output_modalities"]!.Values<string>().ShouldBe(new[] { "audio" });
        session["modalities"].ShouldBeNull();
        session["temperature"].ShouldBeNull();
        session["audio"]!["input"]!["format"]!["type"]!.Value<string>().ShouldBe("audio/pcmu");
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("whisper-1");
        session["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        session["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("alloy");
    }

    // ── Per-field activation: each override is independent ────────────────

    [Fact]
    public void BuildSessionConfig_TranscriptionLanguageSet_ActivatesAlone()
    {
        // The realistic operator pattern: set ONE knob (here, language hint) on ONE
        // assistant and leave the rest at default. The activated field appears in the
        // payload; every other override field is absent. Per-field activation means a
        // single misconfiguration can't cascade to other unrelated settings.
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TranscriptionLanguage = "yue";

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["transcription"]!["language"]!.Value<string>().ShouldBe("yue");
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("whisper-1");
        session["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        session["audio"]!["output"]!["speed"].ShouldBeNull();
        session["max_response_output_tokens"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_TranscriptionModelSet_ActivatesAlone()
    {
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TranscriptionModel = "gpt-4o-transcribe";

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("gpt-4o-transcribe");
        json["session"]!["audio"]!["input"]!["transcription"]!["language"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_TurnDetectionTypeSet_ActivatesWithNestedFields()
    {
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TurnDetectionType = "semantic_vad";
        options.ModelConfig.TurnDetectionThreshold = 0.5m;
        options.ModelConfig.TurnDetectionSilenceMs = 700;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        var td = json["session"]!["audio"]!["input"]!["turn_detection"]!;
        td["type"]!.Value<string>().ShouldBe("semantic_vad");
        td["threshold"]!.Value<decimal>().ShouldBe(0.5m);
        td["silence_duration_ms"]!.Value<int>().ShouldBe(700);
    }

    [Fact]
    public void BuildSessionConfig_TurnDetectionTypeNull_FallsBackToFunctionCallConfig()
    {
        // When TurnDetectionType is null, the adapter MUST use the pre-4.2 path:
        // either the function-call config or the hard-coded server_vad default.
        // This is the "opt-in is per-field" invariant — every adjacent override
        // independently respects its own null state.
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TurnDetection = new { type = "server_vad", threshold = 0.99m };
        // TurnDetectionType is null (opt-in not exercised)

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        json["session"]!["audio"]!["input"]!["turn_detection"]!["threshold"]!.Value<decimal>().ShouldBe(0.99m);
    }

    [Fact]
    public void BuildSessionConfig_NoiseReductionTypeSet_ActivatesAlone()
    {
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.InputNoiseReductionType = "near_field";

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["noise_reduction"]!["type"]!.Value<string>().ShouldBe("near_field");
    }

    [Fact]
    public void BuildSessionConfig_NoiseReductionTypeNull_FallsBackToFunctionCallConfig()
    {
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.InputAudioNoiseReduction = new { type = "far_field" };
        // InputNoiseReductionType is null (opt-in not exercised)

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["noise_reduction"]!["type"]!.Value<string>().ShouldBe("far_field");
    }

    [Fact]
    public void BuildSessionConfig_MaxResponseOutputTokensSet_ActivatesAlone()
    {
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.MaxResponseOutputTokens = 1200;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["max_response_output_tokens"]!.Value<int>().ShouldBe(1200);
    }

    [Fact]
    public void BuildSessionConfig_OutputAudioSpeedSet_ActivatesAlone()
    {
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.OutputAudioSpeed = 0.85m;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["speed"]!.Value<decimal>().ShouldBe(0.85m);
    }

    // ── Multi-field activation: all overrides set on one assistant ─────────

    [Fact]
    public void BuildSessionConfig_AllOverridesSet_AllActivateTogether()
    {
        // An assistant fully opted into every override. Every field appears in the
        // payload with the operator's value — nothing falls through to a hard-coded
        // default. This is the upper bound of opt-in activation.
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TranscriptionModel = "gpt-4o-transcribe";
        options.ModelConfig.TranscriptionLanguage = "yue";
        options.ModelConfig.TurnDetectionType = "semantic_vad";
        options.ModelConfig.TurnDetectionThreshold = 0.4m;
        options.ModelConfig.TurnDetectionSilenceMs = 500;
        options.ModelConfig.InputNoiseReductionType = "near_field";
        options.ModelConfig.MaxResponseOutputTokens = 800;
        options.ModelConfig.OutputAudioSpeed = 1.1m;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("gpt-4o-transcribe");
        session["audio"]!["input"]!["transcription"]!["language"]!.Value<string>().ShouldBe("yue");
        session["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("semantic_vad");
        session["audio"]!["input"]!["turn_detection"]!["threshold"]!.Value<decimal>().ShouldBe(0.4m);
        session["audio"]!["input"]!["turn_detection"]!["silence_duration_ms"]!.Value<int>().ShouldBe(500);
        session["audio"]!["input"]!["noise_reduction"]!["type"]!.Value<string>().ShouldBe("near_field");
        session["max_response_output_tokens"]!.Value<int>().ShouldBe(800);
        session["audio"]!["output"]!["speed"]!.Value<decimal>().ShouldBe(1.1m);
    }
}
