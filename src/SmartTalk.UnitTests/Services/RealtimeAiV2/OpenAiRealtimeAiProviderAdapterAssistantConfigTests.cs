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
/// session-config overrides. The single load-bearing invariant: when the env var is unset
/// (<see cref="SmartTalk.Messages.Hardening.EnforcementMode.Off"/>, the default) OR when
/// every <c>RealtimeAiModelConfig</c> override field is null, the serialised JSON must be
/// **byte-equivalent** to the pre-4.2 output. This guards the "no breaking changes" promise
/// of the Round 2 rollout.
///
/// <para>
/// Tests run with <see cref="JsonSerializerSettings"/> that mirror the production caller
/// (<c>NullValueHandling.Ignore</c>) — null override fields are stripped at serialisation
/// time, which is what makes "all-null produces byte-equivalent output" work.
/// </para>
/// </summary>
[Collection("EnvVarSerial")]   // serialised because tests mutate a process-global env var
public class OpenAiRealtimeAiProviderAdapterAssistantConfigTests : IDisposable
{
    private readonly string _envVarOriginalValue;

    public OpenAiRealtimeAiProviderAdapterAssistantConfigTests()
    {
        _envVarOriginalValue = Environment.GetEnvironmentVariable(OpenAiRealtimeAiProviderAdapter.AssistantConfigEnforcementEnvVar);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(OpenAiRealtimeAiProviderAdapter.AssistantConfigEnforcementEnvVar, _envVarOriginalValue);
    }

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

    private void SetEnv(string value) =>
        Environment.SetEnvironmentVariable(OpenAiRealtimeAiProviderAdapter.AssistantConfigEnforcementEnvVar, value);

    // ── Rule 8: env-var name pinning ───────────────────────────────────────

    [Fact]
    public void AssistantConfigEnforcementEnvVar_ConstantNameIsPinned()
    {
        // Renaming this env var breaks every operator who pinned this flag via the
        // environment. Hard-pin the literal so a rename becomes a compile-time-visible
        // decision rather than an invisible refactor.
        OpenAiRealtimeAiProviderAdapter.AssistantConfigEnforcementEnvVar
            .ShouldBe("SQUID_SMARTTALK_REALTIME_ASSISTANT_CONFIG_ENFORCEMENT");
    }

    // ── Default mode (env var unset) → byte-equivalent to pre-4.2 ──────────

    [Fact]
    public void BuildSessionConfig_EnvVarUnset_PayloadByteEquivalentToPreFour()
    {
        // The load-bearing invariant: with the env var unset and overrides set on the
        // model config, the override fields MUST be ignored. This is what makes
        // 4.2's deploy zero-impact even if Phase 4.1 left non-null values on some row.
        SetEnv(null);
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();

        // Populate overrides to prove they're ignored.
        options.ModelConfig.TranscriptionModel = "gpt-4o-transcribe";
        options.ModelConfig.TranscriptionLanguage = "yue";
        options.ModelConfig.TurnDetectionType = "semantic_vad";
        options.ModelConfig.TurnDetectionThreshold = 0.5m;
        options.ModelConfig.TurnDetectionSilenceMs = 700;
        options.ModelConfig.InputNoiseReductionType = "near_field";
        options.ModelConfig.MaxResponseOutputTokens = 1200;
        options.ModelConfig.OutputAudioSpeed = 1.15m;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

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

    [Theory]
    [InlineData("off")]
    [InlineData("disabled")]
    [InlineData("0")]
    [InlineData("false")]
    public void BuildSessionConfig_EnvVarOffAlias_PayloadByteEquivalentToPreFour(string envValue)
    {
        // Operators who explicitly want "no per-assistant overrides" can use any of
        // these aliases. All MUST behave identically to the unset case — assert the same
        // depth as BuildSessionConfig_EnvVarUnset_PayloadByteEquivalentToPreFour so a
        // regression in any single off-alias gets caught, not just the easily-checked
        // language / speed fields.
        SetEnv(envValue);
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();

        // Populate every override field so any missed null-check would visibly leak.
        options.ModelConfig.TranscriptionModel = "gpt-4o-transcribe";
        options.ModelConfig.TranscriptionLanguage = "zh";
        options.ModelConfig.TurnDetectionType = "semantic_vad";
        options.ModelConfig.TurnDetectionThreshold = 0.5m;
        options.ModelConfig.TurnDetectionSilenceMs = 700;
        options.ModelConfig.InputNoiseReductionType = "near_field";
        options.ModelConfig.MaxResponseOutputTokens = 1200;
        options.ModelConfig.OutputAudioSpeed = 1.15m;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

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
    public void BuildSessionConfig_EnvVarUnset_AllOriginalGaFieldsPreserved()
    {
        // Sanity-check that no Phase 4.2 wiring perturbed the existing GA contract.
        // If any of these break, Phase 4.2 is silently breaking already-deployed
        // assistants — the entire round-2 promise depends on this staying green.
        SetEnv(null);
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

    // ── Warn / strict mode + non-null overrides → fields activated ────────

    [Theory]
    [InlineData("warn")]
    [InlineData("strict")]
    [InlineData("enforce")]
    [InlineData("1")]
    public void BuildSessionConfig_EnvVarEnabled_TranscriptionLanguageActivates(string envValue)
    {
        SetEnv(envValue);
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TranscriptionLanguage = "yue";

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["transcription"]!["language"]!.Value<string>().ShouldBe("yue");
    }

    [Fact]
    public void BuildSessionConfig_EnvVarEnabled_TranscriptionModelOverrideActivates()
    {
        SetEnv("warn");
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TranscriptionModel = "gpt-4o-transcribe";

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("gpt-4o-transcribe");
    }

    [Fact]
    public void BuildSessionConfig_EnvVarEnabled_TurnDetectionTypeActivatesWithThresholdAndSilence()
    {
        SetEnv("warn");
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
    public void BuildSessionConfig_EnvVarEnabled_TurnDetectionTypeNull_FallsBackToFunctionCallConfig()
    {
        // Even with enforcement enabled, an assistant that hasn't opted in (TurnDetectionType
        // is null) MUST get the pre-4.2 fallback: either the function-call config or default
        // server_vad. This is the "opt-in is per-field" invariant — Phase 5 picks one field
        // at a time, never auto-activates everything.
        SetEnv("warn");
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.TurnDetection = new { type = "server_vad", threshold = 0.99m };
        // TurnDetectionType is null (opt-in not exercised)

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        json["session"]!["audio"]!["input"]!["turn_detection"]!["threshold"]!.Value<decimal>().ShouldBe(0.99m);
    }

    [Fact]
    public void BuildSessionConfig_EnvVarEnabled_NoiseReductionTypeActivates()
    {
        SetEnv("warn");
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.InputNoiseReductionType = "near_field";

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["noise_reduction"]!["type"]!.Value<string>().ShouldBe("near_field");
    }

    [Fact]
    public void BuildSessionConfig_EnvVarEnabled_NoiseReductionNull_FallsBackToFunctionCallConfig()
    {
        SetEnv("warn");
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.InputAudioNoiseReduction = new { type = "far_field" };
        // InputNoiseReductionType is null (opt-in not exercised)

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["noise_reduction"]!["type"]!.Value<string>().ShouldBe("far_field");
    }

    [Fact]
    public void BuildSessionConfig_EnvVarEnabled_MaxResponseOutputTokensActivates()
    {
        SetEnv("warn");
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.MaxResponseOutputTokens = 1200;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["max_response_output_tokens"]!.Value<int>().ShouldBe(1200);
    }

    [Fact]
    public void BuildSessionConfig_EnvVarEnabled_OutputAudioSpeedActivates()
    {
        SetEnv("warn");
        var adapter = NewAdapter();
        var options = OptionsWithDefaults();
        options.ModelConfig.OutputAudioSpeed = 0.85m;

        var json = SerializeSessionAsProduction(adapter.BuildSessionConfig(options, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["speed"]!.Value<decimal>().ShouldBe(0.85m);
    }

    [Fact]
    public void BuildSessionConfig_EnvVarEnabled_AllConfigNull_StillByteEquivalentToPreFour()
    {
        // The other half of the "no breaking" promise: even with enforcement enabled,
        // an assistant that has not configured ANY overrides (every column is NULL —
        // the realistic state for every existing prod row) MUST still produce a
        // byte-equivalent session.update payload.
        SetEnv("warn");
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
}
