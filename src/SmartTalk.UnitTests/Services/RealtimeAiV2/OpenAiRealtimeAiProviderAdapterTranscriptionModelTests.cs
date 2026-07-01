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
/// Per-assistant transcription model override contract tests for
/// <see cref="OpenAiRealtimeAiProviderAdapter.BuildSessionConfig"/>.
///
/// <para>
/// Two load-bearing invariants:
/// <list type="bullet">
///   <item>
///     When <see cref="RealtimeAiModelConfig.TranscriptionModel"/> is null or empty
///     (the realistic state for every assistant with no TranscriptionModel row),
///     the adapter emits <see cref="OpenAiRealtimeAiProviderAdapter.DefaultTranscriptionModel"/>.
///     This is what makes the Phase 5.4 default-upgrade safe: every existing prod
///     assistant transparently gets the upgraded model on deploy.
///   </item>
///   <item>
///     When an operator sets a non-null TranscriptionModel value, the adapter emits
///     it verbatim — no allow-list, no silent substitution. OpenAI rejects unknown
///     values server-side rather than the adapter falling back to the default.
///   </item>
/// </list>
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterTranscriptionModelTests
{
    private static readonly JsonSerializerSettings ProductionSerializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeSessionOptions OptionsWithModel(string model) =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                VendorOptions = new OpenAiRealtimeModelOptions { TranscriptionModel = model }
            }
        };

    private static JObject SerializeAsProduction(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload, ProductionSerializer));

    // ── Compile-time default ───────────────────────────────────────────────

    [Fact]
    public void DefaultTranscriptionModel_PinIsCurrentStrongestModel()
    {
        // Hard-pinned literal so a future default change is a deliberate,
        // visible decision rather than an invisible refactor. As of 2026-05-19
        // gpt-4o-transcribe is OpenAI's most capable transcription model and is
        // priced identically to whisper-1 ($0.006/min) — strict quality upgrade,
        // zero cost change. If/when OpenAI ships a stronger default, update this
        // pin together with the production constant.
        OpenAiRealtimeAiProviderAdapter.DefaultTranscriptionModel.ShouldBe("gpt-4o-transcribe");
    }

    // ── Default path: null / empty TranscriptionModel → adapter default ────

    [Fact]
    public void BuildSessionConfig_TranscriptionModelNull_UsesDefaultModel()
    {
        // The realistic prod state: no TranscriptionModel row → ModelConfig.TranscriptionModel
        // is null → adapter falls back to DefaultTranscriptionModel. This is the deploy-day
        // behaviour for every existing assistant.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithModel(null), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["transcription"]!["model"]!.Value<string>()
            .ShouldBe(OpenAiRealtimeAiProviderAdapter.DefaultTranscriptionModel);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void BuildSessionConfig_TranscriptionModelEmptyOrWhitespace_UsesDefaultModel(string model)
    {
        // Defence: even if the service-side parser ever lets through an empty / whitespace
        // value (current parser strips it), the adapter falls back to default instead of
        // sending `"model": ""` and getting rejected by OpenAI.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithModel(model), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["transcription"]!["model"]!.Value<string>()
            .ShouldBe(OpenAiRealtimeAiProviderAdapter.DefaultTranscriptionModel);
    }

    // ── Active override path: operator-supplied model ──────────────────────

    [Theory]
    [InlineData("whisper-1")]                  // legacy downgrade (e.g. cost-sensitive assistant)
    [InlineData("gpt-4o-mini-transcribe")]     // cheaper variant
    [InlineData("gpt-4o-transcribe")]          // explicit re-affirm of the default
    public void BuildSessionConfig_TranscriptionModelSet_EmitsOperatorValue(string model)
    {
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithModel(model), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe(model);
    }

    [Fact]
    public void BuildSessionConfig_TranscriptionModelFutureValue_PassesThroughVerbatim()
    {
        // Forward-compatibility: an operator can opt into a future OpenAI model
        // (e.g. `gpt-5-transcribe`) without a code change. The adapter does not
        // validate the string — OpenAI rejects unknown values server-side rather
        // than the adapter silently falling back to default.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithModel("gpt-5-transcribe"), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["input"]!["transcription"]!["model"]!.Value<string>().ShouldBe("gpt-5-transcribe");
    }

    // ── Coexistence with TranscriptionLanguage ─────────────────────────────

    [Fact]
    public void BuildSessionConfig_BothModelAndLanguageSet_BothActivateIndependently()
    {
        // The two per-assistant configs are independent dimensions: an operator can
        // set model alone, language alone, or both. This test pins the combined shape.
        var adapter = NewAdapter();
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>(),
                VendorOptions = new OpenAiRealtimeModelOptions
                {
                    TranscriptionModel = "gpt-4o-mini-transcribe",
                    TranscriptionLanguage = "yue"
                }
            }
        };

        var json = SerializeAsProduction(adapter.BuildSessionConfig(options, RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        var transcription = json["session"]!["audio"]!["input"]!["transcription"]!;
        transcription["model"]!.Value<string>().ShouldBe("gpt-4o-mini-transcribe");
        transcription["language"]!.Value<string>().ShouldBe("yue");
    }

    [Fact]
    public void BuildSessionConfig_TranscriptionModelSet_DoesNotPerturbAdjacentFields()
    {
        // Activating the model override must not silently shift turn_detection,
        // noise_reduction, voice, or output format.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithModel("whisper-1"), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        var session = json["session"]!;
        session["audio"]!["input"]!["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
        session["audio"]!["input"]!["noise_reduction"].ShouldBeNull();
        session["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("alloy");
        session["audio"]!["output"]!["format"]!["type"]!.Value<string>().ShouldBe("audio/pcmu");
    }
}
