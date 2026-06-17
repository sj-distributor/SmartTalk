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
/// Pins the OpenAI Realtime API voice list and the default-voice constant.
///
/// <para>
/// The literals are mirrored from OpenAI's documented voice options. The pin exists
/// so any drift between the OpenAI docs and our list is a visible code change rather
/// than a silent dropdown-population bug, and so UI / operator tooling has a single
/// source of truth.
/// </para>
///
/// <para>
/// The adapter does NOT enforce membership — operators may opt into a future OpenAI
/// voice without a code change, and OpenAI rejects unknown values server-side. The
/// last test pins the pass-through behaviour so we never silently substitute.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterVoiceListTests
{
    private static readonly JsonSerializerSettings ProductionSerializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeSessionOptions OptionsWithVoice(string voice) =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = voice,
                Tools = new List<object>()
            }
        };

    private static JObject SerializeAsProduction(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload, ProductionSerializer));

    // ── Constants pinned ───────────────────────────────────────────────────

    [Fact]
    public void DefaultVoice_IsAlloy()
    {
        // The historical default is "alloy". Changing it is a deliberate decision —
        // every assistant without an explicit Voice override would switch to the new
        // value on deploy. Hard-pin the literal so the change is visible in code review.
        OpenAiRealtimeAiProviderAdapter.DefaultVoice.ShouldBe("alloy");
    }

    [Fact]
    public void SupportedVoices_ContainsExactly13DocumentedVoices()
    {
        // Source: https://platform.openai.com/docs/guides/realtime as of 2026-05-20.
        // Order matches a stable ID alphabetisation so a diff against this assertion
        // makes the addition / removal obvious in code review.
        OpenAiRealtimeAiProviderAdapter.SupportedVoices.ShouldBe(new[]
        {
            "alloy",
            "ash",
            "ballad",
            "coral",
            "echo",
            "fable",
            "onyx",
            "nova",
            "sage",
            "shimmer",
            "verse",
            "marin",
            "cedar"
        });
    }

    [Fact]
    public void SupportedVoices_ContainsTheDefaultVoice()
    {
        // The default we send must be one of the values OpenAI accepts — otherwise
        // every assistant without an explicit Voice override would fail at session.update.
        OpenAiRealtimeAiProviderAdapter.SupportedVoices.ShouldContain(OpenAiRealtimeAiProviderAdapter.DefaultVoice);
    }

    [Fact]
    public void SupportedVoices_HasNoDuplicates()
    {
        // Defensive: a duplicate would not break anything, but it suggests sloppy
        // editing when the list was last updated. Catch at test time, not in review.
        OpenAiRealtimeAiProviderAdapter.SupportedVoices.Distinct().Count()
            .ShouldBe(OpenAiRealtimeAiProviderAdapter.SupportedVoices.Count);
    }

    // ── Adapter wires the default + passes through every list entry ───────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildSessionConfig_VoiceNullOrEmpty_UsesDefault(string voice)
    {
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithVoice(voice), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["voice"]!.Value<string>()
            .ShouldBe(OpenAiRealtimeAiProviderAdapter.DefaultVoice);
    }

    [Theory]
    [InlineData("alloy")]
    [InlineData("ash")]
    [InlineData("ballad")]
    [InlineData("coral")]
    [InlineData("echo")]
    [InlineData("fable")]
    [InlineData("onyx")]
    [InlineData("nova")]
    [InlineData("sage")]
    [InlineData("shimmer")]
    [InlineData("verse")]
    [InlineData("marin")]
    [InlineData("cedar")]
    public void BuildSessionConfig_EveryDocumentedVoice_EmittedVerbatim(string voice)
    {
        // Each documented voice must pass through the adapter unchanged. Failure here
        // would mean the adapter silently coerced an operator-selected voice into a
        // different one — a UX regression hard to debug from logs alone.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithVoice(voice), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe(voice);
    }

    [Fact]
    public void BuildSessionConfig_UnknownVoice_PassesThroughVerbatim()
    {
        // Forward-compatibility: when OpenAI adds a new voice (e.g. "midnight"),
        // operators can opt into it immediately. The adapter does NOT validate the
        // string — OpenAI rejects unknown values server-side with a clear error,
        // which is strictly better than the adapter silently swapping in DefaultVoice
        // and leaving the operator wondering why their selection had no effect.
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithVoice("midnight"), RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json["session"]!["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("midnight");
    }
}
