using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Core.Services.RealtimeAi.wss.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.UnitTests.Services.RealtimeAiV2;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAi;

/// <summary>
/// Pins the V1 OpenAI Realtime adapter to the GA contract (post 2026-05-07).
/// The V1 path is not exercised in production today (V2 carries all traffic), but
/// keeping V1 on the same contract prevents `invalid_beta` regressions if it is
/// ever reactivated and keeps the two adapters in lockstep.
/// </summary>
public class OpenAiRealtimeAiAdapterGaPayloadTests
{
    private static OpenAiRealtimeAiAdapter NewAdapter(IAiSpeechAssistantDataProvider dataProvider = null)
    {
        var settings = new OpenAiSettings(OpenAiRealtimeAiProviderAdapterTestSettings.BuildConfiguration());
        var provider = dataProvider ?? Substitute.For<IAiSpeechAssistantDataProvider>();

        // Default: no knowledge entry, no function calls, no caches.
        provider.GetAiSpeechAssistantKnowledgeAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns((AiSpeechAssistantKnowledge)null);
        provider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(Arg.Any<List<int>>(), Arg.Any<RealtimeAiProvider>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantFunctionCall>());

        return new OpenAiRealtimeAiAdapter(settings, provider);
    }

    private static RealtimeSessionOptions OptionsWithCodec(RealtimeAiAudioCodec codec, string voice = "alloy", string prompt = "you are helpful") =>
        new()
        {
            InputFormat = codec,
            OutputFormat = codec,
            InitialPrompt = prompt,
            ConnectionProfile = new RealtimeAiConnectionProfile { ProfileId = "1" },
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                Voice = voice
            }
        };

    private static async Task<JObject> SerializeSessionAsync(OpenAiRealtimeAiAdapter adapter, RealtimeSessionOptions options) =>
        JObject.Parse(JsonConvert.SerializeObject(await adapter.GetInitialSessionPayloadAsync(options, "sess-1", CancellationToken.None)));

    // ── Headers ────────────────────────────────────────────────────

    [Fact]
    public void GetHeaders_DoesNotIncludeBetaHeader_AfterGaCutover()
    {
        var adapter = NewAdapter();

        var headers = adapter.GetHeaders(RealtimeAiServerRegion.US);

        headers.ShouldNotContainKey("OpenAI-Beta");
        headers.ShouldContainKey("Authorization");
    }

    // ── Session payload top-level shape ────────────────────────────

    [Fact]
    public async Task GetInitialSessionPayload_TopLevel_HasSessionTypeRealtime()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW));

        json["type"]!.Value<string>().ShouldBe("session.update");
        json["session"]!["type"]!.Value<string>().ShouldBe("realtime");
    }

    [Fact]
    public async Task GetInitialSessionPayload_UsesOutputModalities_NotLegacyModalities()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW));

        json["session"]!["modalities"].ShouldBeNull();
        json["session"]!["output_modalities"]!.Values<string>().ShouldBe(new[] { "audio" });
    }

    [Fact]
    public async Task GetInitialSessionPayload_DoesNotEmitTemperature_AfterGaCutover()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW));

        json["session"]!["temperature"].ShouldBeNull();
    }

    [Fact]
    public async Task GetInitialSessionPayload_DoesNotEmitFlatAudioFields_AfterGaCutover()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW));

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
    public async Task GetInitialSessionPayload_G711Codec_FormatHasTypeOnly_NoRateField(RealtimeAiAudioCodec codec, string expectedType)
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(codec));

        var inputFormat = json["session"]!["audio"]!["input"]!["format"]!;
        inputFormat["type"]!.Value<string>().ShouldBe(expectedType);
        inputFormat["rate"].ShouldBeNull();

        var outputFormat = json["session"]!["audio"]!["output"]!["format"]!;
        outputFormat["type"]!.Value<string>().ShouldBe(expectedType);
        outputFormat["rate"].ShouldBeNull();
    }

    [Fact]
    public async Task GetInitialSessionPayload_Pcm16Codec_FormatCarriesExplicitRate()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.PCM16));

        var inputFormat = json["session"]!["audio"]!["input"]!["format"]!;
        inputFormat["type"]!.Value<string>().ShouldBe("audio/pcm");
        inputFormat["rate"]!.Value<int>().ShouldBe(24000);

        var outputFormat = json["session"]!["audio"]!["output"]!["format"]!;
        outputFormat["type"]!.Value<string>().ShouldBe("audio/pcm");
        outputFormat["rate"]!.Value<int>().ShouldBe(24000);
    }

    // ── Nested audio config is preserved ────────────────────────────

    [Fact]
    public async Task GetInitialSessionPayload_AudioInput_CarriesTranscriptionAndTurnDetection()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW));

        var input = json["session"]!["audio"]!["input"]!;
        input["transcription"]!["model"]!.Value<string>().ShouldBe("whisper-1");
        input["turn_detection"]!["type"]!.Value<string>().ShouldBe("server_vad");
    }

    [Fact]
    public async Task GetInitialSessionPayload_AudioOutput_CarriesVoice()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW, voice: "shimmer"));

        json["session"]!["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("shimmer");
    }

    [Fact]
    public async Task GetInitialSessionPayload_VoiceFallsBackToAlloy_WhenUnset()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW, voice: ""));

        json["session"]!["audio"]!["output"]!["voice"]!.Value<string>().ShouldBe("alloy");
    }

    [Fact]
    public async Task GetInitialSessionPayload_PreservesInstructionsAtSessionLevel()
    {
        var adapter = NewAdapter();

        var json = await SerializeSessionAsync(adapter, OptionsWithCodec(RealtimeAiAudioCodec.MULAW, prompt: "你是收銀員"));

        json["session"]!["instructions"]!.Value<string>().ShouldBe("你是收銀員");
    }

    // ── Event-name parsing accepts both old and new names ──────────

    [Theory]
    [InlineData("response.audio.delta")]
    [InlineData("response.output_audio.delta")]
    public void ParseMessage_BothAudioDeltaNames_MapToResponseAudioDelta(string eventType)
    {
        var adapter = NewAdapter();
        var raw = JsonConvert.SerializeObject(new { type = eventType, delta = "AQID", item_id = "itm-1" });

        var parsed = adapter.ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDelta);
    }

    [Theory]
    [InlineData("response.audio.done")]
    [InlineData("response.output_audio.done")]
    public void ParseMessage_BothAudioDoneNames_MapToResponseAudioDone(string eventType)
    {
        var adapter = NewAdapter();
        var raw = JsonConvert.SerializeObject(new { type = eventType, item_id = "itm-1" });

        var parsed = adapter.ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDone);
    }

    [Theory]
    [InlineData("response.audio_transcript.delta")]
    [InlineData("response.output_audio_transcript.delta")]
    public void ParseMessage_BothAudioTranscriptDeltaNames_MapToOutputAudioTranscriptionPartial(string eventType)
    {
        var adapter = NewAdapter();
        var raw = JsonConvert.SerializeObject(new { type = eventType, delta = "partial" });

        var parsed = adapter.ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.OutputAudioTranscriptionPartial);
    }

    [Theory]
    [InlineData("response.audio_transcript.done")]
    [InlineData("response.output_audio_transcript.done")]
    public void ParseMessage_BothAudioTranscriptDoneNames_MapToOutputAudioTranscriptionCompleted(string eventType)
    {
        var adapter = NewAdapter();
        var raw = JsonConvert.SerializeObject(new { type = eventType, transcript = "hi" });

        var parsed = adapter.ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.OutputAudioTranscriptionCompleted);
    }
}
