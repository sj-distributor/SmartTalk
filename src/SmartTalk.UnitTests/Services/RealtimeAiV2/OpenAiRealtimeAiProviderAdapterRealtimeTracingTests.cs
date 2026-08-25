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
/// Per-assistant OpenAI session-tracing contract tests for
/// <see cref="OpenAiRealtimeAiProviderAdapter.BuildSessionConfig"/>.
///
/// <para>
/// The load-bearing invariant: when <see cref="RealtimeAiModelConfig.EnableRealtimeTracing"/>
/// is null or false (the realistic state for every assistant with no opt-in
/// row), the <c>session</c> object MUST NOT contain a <c>tracing</c> property
/// — so OpenAI does NOT retain a session trace, matching the pre-feature
/// privacy posture.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterRealtimeTracingTests
{
    private static readonly JsonSerializerSettings ProductionSerializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeSessionOptions OptionsWithTracing(bool? enableTracing) =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                VendorOptions = new OpenAiRealtimeModelOptions { EnableRealtimeTracing = enableTracing }
            }
        };

    private static JObject SerializeAsProduction(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload, ProductionSerializer));

    // ── Compile-time mode constant pinned ─────────────────────────────────

    [Fact]
    public void EnabledTracingMode_PinnedToAuto()
    {
        // The wire literal under session.tracing is hard-pinned so a future
        // rename / typo is a visible compile-time decision rather than an
        // invisible refactor.
        OpenAiRealtimeAiProviderAdapter.EnabledTracingMode.ShouldBe("auto");
    }

    // ── Default path: null / false → field absent ──────────────────────────

    [Fact]
    public void BuildSessionConfig_TracingNull_FieldAbsentFromPayload()
    {
        // Load-bearing: every existing prod assistant has no RealtimeTracing row →
        // EnableRealtimeTracing is null → NullValueHandling.Ignore strips the
        // `tracing` key from session. OpenAI does not retain a trace.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithTracing(null), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["tracing"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_TracingFalse_FieldAbsentFromPayload()
    {
        // An explicit `false` MUST behave identically to null — operators can
        // persist a row with `{ "enabled": false }` to deliberately document
        // "we considered tracing and decided against it"; that must not surface
        // as an enabled trace.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithTracing(false), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["tracing"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_TracingNull_AdjacentSessionFieldsUnchanged()
    {
        // Sanity: null tracing must not perturb any adjacent session-level field.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithTracing(null), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["type"]!.Value<string>().ShouldBe("realtime");
        session["instructions"]!.Value<string>().ShouldBe("you are helpful");
        session["output_modalities"]!.Values<string>().ShouldBe(new[] { "audio" });
        session["tracing"].ShouldBeNull();
    }

    // ── Active opt-in: true → emits "auto" at session level ────────────────

    [Fact]
    public void BuildSessionConfig_TracingTrue_EmitsAutoAtSessionLevel()
    {
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithTracing(true), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["tracing"]!.Value<string>().ShouldBe(OpenAiRealtimeAiProviderAdapter.EnabledTracingMode);
    }

    [Fact]
    public void BuildSessionConfig_TracingTrue_DoesNotPerturbAudioFields()
    {
        // Activating tracing must not silently shift any audio field —
        // tracing is purely an out-of-band metadata flag.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithTracing(true), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>()
            .ShouldBe(OpenAiRealtimeAiProviderAdapter.DefaultTranscriptionModel);
        session["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        session["audio"]!["input"]!["noise_reduction"].ShouldBeNull();
        session["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("alloy");
        session["audio"]!["output"]!["format"]!["type"]!.Value<string>().ShouldBe("audio/pcmu");
    }

    // ── Coexistence with adjacent per-assistant configs ────────────────────

    [Fact]
    public void BuildSessionConfig_TracingAndLanguageAndModelAndCap_AllActivateIndependently()
    {
        // Pin the combined shape so a future PR that touches one Phase 5 config
        // cannot silently break adjacent ones.
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                VendorOptions = new OpenAiRealtimeModelOptions
                {
                    TranscriptionLanguage = "yue",
                    TranscriptionModel = "gpt-4o-mini-transcribe",
                    MaxResponseOutputTokens = 1200,
                    EnableRealtimeTracing = true
                }
            }
        };

        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(options, RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("gpt-4o-mini-transcribe");
        session["audio"]!["input"]!["transcription"]!["language"]!.Value<string>().ShouldBe("yue");
        session["max_response_output_tokens"]!.Value<int>().ShouldBe(1200);
        session["tracing"]!.Value<string>().ShouldBe("auto");
    }
}
