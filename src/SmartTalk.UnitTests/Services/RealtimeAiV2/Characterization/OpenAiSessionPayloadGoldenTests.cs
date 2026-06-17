using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION (golden-master) test — pins the EXACT serialized OpenAI session.update
/// payload byte-for-byte, using the production serializer settings
/// (<see cref="JsonConvert"/>.SerializeObject with <see cref="NullValueHandling.Ignore"/>)
/// that RealtimeAiService.Connect.cs:33 uses on the wire.
///
/// Unlike the per-field GA-contract tests (which parse to JObject and are order-insensitive),
/// this asserts ORDINAL STRING equality so that a key REORDER or a newly-introduced opaque key —
/// the realistic regression from the upcoming generic refactor (explicit output-mode param, moving
/// the 8 OpenAI-specific fields off ModelConfig into a vendor bag) — fails RED. The golden literals
/// were captured by running the real production code once and frozen here; they are NOT recomputed
/// with the logic under test. A deliberate wire-format change re-baselines the literal in a commit
/// that names the delta (e.g. a DefaultVoice / DefaultTranscriptionModel change is a wire change and
/// SHOULD fail this test).
///
/// Guards migration steps S1 (TtsConfig relocation), S2 (additive Capabilities),
/// S4 (BuildSessionConfig signature → explicit RealtimeAiOutputMode), S7 (8 fields → vendor bag).
/// </summary>
public class OpenAiSessionPayloadGoldenTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    // The exact serializer the engine uses on the wire — RealtimeAiService.Connect.cs:33.
    private static string ProductionSerialize(object payload) =>
        JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

    // P1 golden — audio mode (BuiltIn TTS: no TtsConfig), MULAW, every optional field null/absent.
    // NullValueHandling.Ignore strips max_response_output_tokens, tracing, language, noise_reduction,
    // speed and tools; output_modalities is ["audio"] (built-in audio path).
    private const string AudioModeAllNullGolden =
        """{"type":"session.update","session":{"type":"realtime","instructions":"you are helpful","output_modalities":["audio"],"audio":{"input":{"format":{"type":"audio/pcmu"},"transcription":{"model":"gpt-4o-transcribe"},"turn_detection":{"type":"server_vad"}},"output":{"format":{"type":"audio/pcmu"},"voice":"alloy"}}}}""";

    // P2 golden — audio mode, MULAW, every optional OpenAI field populated. Pins each field's exact
    // nesting path + value + key order, so moving any of them off ModelConfig (S7) cannot silently
    // relocate a key or co-introduce an extra one.
    private const string AudioModeAllPopulatedGolden =
        """{"type":"session.update","session":{"type":"realtime","instructions":"you are helpful","output_modalities":["audio"],"max_response_output_tokens":4096,"tracing":"auto","audio":{"input":{"format":{"type":"audio/pcmu"},"transcription":{"model":"whisper-1","language":"yue"},"turn_detection":{"type":"semantic_vad"},"noise_reduction":{"type":"near_field"}},"output":{"format":{"type":"audio/pcmu"},"voice":"shimmer","speed":1.2}},"tools":[{"type":"function","name":"lookup_order"}]}}""";

    [Fact]
    public void BuildSessionConfig_AudioMode_AllOptionalNull_IsByteIdentical()
    {
        var adapter = NewAdapter();
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>()
            }
            // TtsConfig null → useExternalTts == false → audio mode.
        };

        var json = ProductionSerialize(adapter.BuildSessionConfig(options, RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json.ShouldBe(AudioModeAllNullGolden);
    }

    [Fact]
    public void BuildSessionConfig_AudioMode_AllOptionalPopulated_IsByteIdentical()
    {
        var adapter = NewAdapter();
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "shimmer",
                Tools = new List<object> { new { type = "function", name = "lookup_order" } },
                TurnDetection = new { type = "semantic_vad" },
                VendorOptions = new OpenAiRealtimeModelOptions
                {
                    InputAudioNoiseReduction = new { type = "near_field" },
                    TranscriptionModel = "whisper-1",
                    TranscriptionLanguage = "yue",
                    MaxResponseOutputTokens = 4096,
                    OutputAudioSpeed = 1.2m,
                    EnableRealtimeTracing = true
                }
            }
        };

        var json = ProductionSerialize(adapter.BuildSessionConfig(options, RealtimeAiOutputMode.Audio, RealtimeAiAudioCodec.MULAW));

        json.ShouldBe(AudioModeAllPopulatedGolden);
    }
}
