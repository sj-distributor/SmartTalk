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
/// Per-assistant response-token cap contract tests for
/// <see cref="OpenAiRealtimeAiProviderAdapter.BuildSessionConfig"/>.
///
/// <para>
/// The load-bearing invariant: when <see cref="RealtimeAiModelConfig.MaxResponseOutputTokens"/>
/// is null (the realistic state for every assistant with no cap row), the
/// <c>session</c> object MUST NOT contain a <c>max_response_output_tokens</c>
/// property — so OpenAI uses its server-side default. This is what makes the
/// feature non-breaking: every existing prod assistant continues to behave
/// exactly as today until an operator inserts a row.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterMaxResponseOutputTokensTests
{
    private static readonly JsonSerializerSettings ProductionSerializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeSessionOptions OptionsWithCap(int? cap) =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                MaxResponseOutputTokens = cap
            }
        };

    private static JObject SerializeAsProduction(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload, ProductionSerializer));

    // ── Default path: null cap → field absent from payload ────────────────

    [Fact]
    public void BuildSessionConfig_MaxResponseOutputTokensNull_FieldAbsentFromPayload()
    {
        // The load-bearing test: every existing prod assistant has no
        // MaxResponseOutputTokens config row, so ModelConfig.MaxResponseOutputTokens
        // is null. The serialised session object MUST contain no
        // `max_response_output_tokens` property — NullValueHandling.Ignore
        // strips it at serialisation time. OpenAI server-side default applies.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithCap(null), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["max_response_output_tokens"].ShouldBeNull();
    }

    [Fact]
    public void BuildSessionConfig_MaxResponseOutputTokensNull_AdjacentSessionFieldsUnchanged()
    {
        // Sanity check: the null-cap path must not perturb any adjacent
        // session-level field. Regression here would mean Phase 5.5 silently
        // shifted instructions / output_modalities / tools semantics.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithCap(null), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["type"]!.Value<string>().ShouldBe("realtime");
        session["instructions"]!.Value<string>().ShouldBe("you are helpful");
        session["output_modalities"]!.Values<string>().ShouldBe(new[] { "audio" });
        session["max_response_output_tokens"].ShouldBeNull();
    }

    // ── Active path: cap set → emitted at session level ────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(800)]
    [InlineData(1200)]
    [InlineData(4096)]
    [InlineData(100000)]
    public void BuildSessionConfig_MaxResponseOutputTokensSet_EmitsCapAtSessionLevel(int cap)
    {
        // The adapter forwards whatever the operator set. No clamping — operators
        // get exact control, and OpenAI rejects values beyond its server-side
        // limits with a clear error rather than the adapter silently clipping.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithCap(cap), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["max_response_output_tokens"]!.Value<int>().ShouldBe(cap);
    }

    [Fact]
    public void BuildSessionConfig_MaxResponseOutputTokensSet_DoesNotPerturbAudioFields()
    {
        // Activating the cap must not silently shift transcription, turn_detection,
        // noise_reduction, voice, or output format.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithCap(1200), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

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
    public void BuildSessionConfig_CapAndLanguageAndModel_AllActivateIndependently()
    {
        // All three Phase 5 per-assistant configs are independent dimensions.
        // Pin the combined shape so a future PR that touches one cannot silently
        // break the others.
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                TranscriptionLanguage = "yue",
                TranscriptionModel = "gpt-4o-mini-transcribe",
                MaxResponseOutputTokens = 1200
            }
        };

        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(options, RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("gpt-4o-mini-transcribe");
        session["audio"]!["input"]!["transcription"]!["language"]!.Value<string>().ShouldBe("yue");
        session["max_response_output_tokens"]!.Value<int>().ShouldBe(1200);
    }
}
