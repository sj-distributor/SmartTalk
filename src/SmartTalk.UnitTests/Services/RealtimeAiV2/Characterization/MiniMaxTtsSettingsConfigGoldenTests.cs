using Microsoft.Extensions.Configuration;
using Shouldly;
using SmartTalk.Core.Settings.MiniMax;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins MiniMaxTtsSettings.BuildRealtimeAiTtsConfig: the exact produced
/// RealtimeAiTtsConfig shape (including the 6-key ProviderSpecificConfig), the two null-return guards
/// (disabled / blank api key), the ResolveMiniMaxVoiceId table, and the source_sample_rate fallback.
/// Config-building is inference-agnostic (S7 removed the modelProvider!=OpenAi gate and its parameter);
/// whether a given inference provider can actually drive external TTS is decided by the engine's
/// OutputModeNegotiator from the provider's declared text-output capability.
/// </summary>
public class MiniMaxTtsSettingsConfigGoldenTests
{
    private static MiniMaxTtsSettings Settings(Action<Dictionary<string, string?>>? customize = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["MiniMaxTts:Enabled"] = "true",
            ["MiniMaxTts:AssistantId"] = "42",
            ["MiniMaxTts:ApiKey"] = "key123",
            ["MiniMaxTts:ServiceUrl"] = "wss://svc",
            ["MiniMaxTts:Model"] = "mdl",
            ["MiniMaxTts:DefaultVoiceId"] = "dv",
            ["MiniMaxTts:SampleRate"] = "8000",
            ["MiniMaxTts:Speed"] = "0.9",
            ["MiniMaxTts:Volume"] = "1.0",
            ["MiniMaxTts:Pitch"] = "2",
            ["MiniMaxTts:Bitrate"] = "128000"
        };
        customize?.Invoke(dict);

        return new MiniMaxTtsSettings(new ConfigurationBuilder().AddInMemoryCollection(dict).Build());
    }

    [Fact]
    public void BuildRealtimeAiTtsConfig_HappyPath_ProducesExactShape()
    {
        var config = Settings().BuildRealtimeAiTtsConfig(42, "custom-voice", sampleRate: 24000);

        config.ShouldNotBeNull();
        config!.ProviderType.ShouldBe(RealtimeAiTtsProviderType.MiniMax);
        config.ServiceUrl.ShouldBe("wss://svc");
        config.ApiKey.ShouldBe("key123");
        config.Voice.ShouldBe("custom-voice");
        config.TargetCodec.ShouldBe(RealtimeAiAudioCodec.PCM16);
        config.SampleRate.ShouldBe(24000);          // the passed arg, not the settings SampleRate

        config.ProviderSpecificConfig.Count.ShouldBe(6);
        config.ProviderSpecificConfig["model"].ShouldBe("mdl");
        config.ProviderSpecificConfig["speed"].ShouldBe(0.9);
        config.ProviderSpecificConfig["vol"].ShouldBe(1.0);
        config.ProviderSpecificConfig["pitch"].ShouldBe(2);
        config.ProviderSpecificConfig["bitrate"].ShouldBe(128000);
        config.ProviderSpecificConfig["source_sample_rate"].ShouldBe(8000);   // null arg → settings.SourceSampleRate (= SampleRate)
    }

    [Fact]
    public void BuildRealtimeAiTtsConfig_ExplicitSourceSampleRate_OverridesSettings()
    {
        var config = Settings().BuildRealtimeAiTtsConfig(42, "v", sampleRate: 24000, sourceSampleRate: 16000);

        config!.ProviderSpecificConfig["source_sample_rate"].ShouldBe(16000);
    }

    [Fact]
    public void BuildRealtimeAiTtsConfig_SourceSampleRateConfigKey_UsedWhenNoArg()
    {
        var config = Settings(d => d["MiniMaxTts:SourceSampleRate"] = "11025")
            .BuildRealtimeAiTtsConfig(42, "v", sampleRate: 24000);

        config!.ProviderSpecificConfig["source_sample_rate"].ShouldBe(11025);
    }

    // ── Null-return guards (precedence order) ──────────────────────

    [Fact]
    public void Guard_DisabledForAssistant_ReturnsNull()
    {
        Settings(d => d["MiniMaxTts:Enabled"] = "false")
            .BuildRealtimeAiTtsConfig(42, "v", 24000).ShouldBeNull();
    }

    [Fact]
    public void Guard_AssistantIdMismatch_ReturnsNull()
    {
        Settings().BuildRealtimeAiTtsConfig(99, "v", 24000).ShouldBeNull();
    }

    // (Removed Guard_NonOpenAiProvider_ReturnsNull: MiniMax config-building is now inference-agnostic.
    //  Whether a given inference provider can drive external TTS is decided by OutputModeNegotiator from
    //  the provider's declared text-output capability — see OutputModeNegotiatorTests.)

    [Fact]
    public void Guard_BlankApiKey_ReturnsNull()
    {
        Settings(d => d["MiniMaxTts:ApiKey"] = "   ")
            .BuildRealtimeAiTtsConfig(42, "v", 24000).ShouldBeNull();
    }

    // ── ResolveMiniMaxVoiceId table ────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveVoice_BlankModelVoice_FallsBackToDefaultVoiceId(string? modelVoice)
    {
        Settings().BuildRealtimeAiTtsConfig(42, modelVoice!, 24000)!.Voice.ShouldBe("dv");
    }

    [Fact]
    public void ResolveVoice_OpenAiVoiceName_RejectedToDefaultVoiceId()
    {
        // An OpenAI voice (in OpenAiRealtimeAiProviderAdapter.SupportedVoices) is not a MiniMax voice,
        // so it falls back to the MiniMax default rather than being forwarded verbatim.
        Settings().BuildRealtimeAiTtsConfig(42, "alloy", 24000)!.Voice.ShouldBe("dv");
    }

    [Fact]
    public void ResolveVoice_NonOpenAiVoiceName_PassedVerbatim()
    {
        Settings().BuildRealtimeAiTtsConfig(42, "Chinese (Mandarin)_News_Anchor", 24000)!
            .Voice.ShouldBe("Chinese (Mandarin)_News_Anchor");
    }
}
