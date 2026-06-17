using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.Config;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;
using SmartTalk.Core.Settings.MiniMax;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the generic TTS config resolution (S8): the resolver returns the first vendor source that
/// applies (null when none do → built-in audio), and MiniMaxTtsConfigSource wraps MiniMaxTtsSettings so
/// the prior consumer behaviour (enablement gating + sample-rate fallback) is preserved exactly. Adding
/// a TTS vendor is then a new IRealtimeAiTtsConfigSource with no consumer or resolver change.
/// </summary>
public class RealtimeAiTtsConfigResolverTests
{
    private static MiniMaxTtsSettings EnabledMiniMaxSettings() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MiniMaxTts:Enabled"] = "true",
            ["MiniMaxTts:AssistantId"] = "42",
            ["MiniMaxTts:ApiKey"] = "key123",
            ["MiniMaxTts:SampleRate"] = "8000"
        }).Build());

    [Fact]
    public void Resolve_ReturnsFirstApplicableSourceConfig()
    {
        var expected = new RealtimeAiTtsConfig { ProviderType = RealtimeAiTtsProviderType.MiniMax };

        var notApplicable = Substitute.For<IRealtimeAiTtsConfigSource>();
        notApplicable.Build(Arg.Any<RealtimeAiTtsRequest>()).Returns((RealtimeAiTtsConfig)null);

        var applicable = Substitute.For<IRealtimeAiTtsConfigSource>();
        applicable.Build(Arg.Any<RealtimeAiTtsRequest>()).Returns(expected);

        var resolver = new RealtimeAiTtsConfigResolver(new[] { notApplicable, applicable });

        resolver.Resolve(new RealtimeAiTtsRequest { AssistantId = 1 }).ShouldBeSameAs(expected);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoSourceApplies()
    {
        var source = Substitute.For<IRealtimeAiTtsConfigSource>();
        source.Build(Arg.Any<RealtimeAiTtsRequest>()).Returns((RealtimeAiTtsConfig)null);

        new RealtimeAiTtsConfigResolver(new[] { source }).Resolve(new RealtimeAiTtsRequest { AssistantId = 1 }).ShouldBeNull();
    }

    [Fact]
    public void MiniMaxConfigSource_Enabled_BuildsConfig_AndDefaultsSampleRateToSettings()
    {
        var source = new MiniMaxTtsConfigSource(EnabledMiniMaxSettings());

        source.ProviderType.ShouldBe(RealtimeAiTtsProviderType.MiniMax);

        var config = source.Build(new RealtimeAiTtsRequest { AssistantId = 42, ModelVoice = "v" });

        config.ShouldNotBeNull();
        config!.ProviderType.ShouldBe(RealtimeAiTtsProviderType.MiniMax);
        config.SampleRate.ShouldBe(8000);   // request had no SampleRate → falls back to settings.SampleRate
    }

    [Fact]
    public void MiniMaxConfigSource_ExplicitSampleRate_Overrides()
    {
        var config = new MiniMaxTtsConfigSource(EnabledMiniMaxSettings())
            .Build(new RealtimeAiTtsRequest { AssistantId = 42, ModelVoice = "v", SampleRate = 24000 });

        config!.SampleRate.ShouldBe(24000);
    }

    [Fact]
    public void MiniMaxConfigSource_NotEnabledForAssistant_ReturnsNull()
    {
        new MiniMaxTtsConfigSource(EnabledMiniMaxSettings())
            .Build(new RealtimeAiTtsRequest { AssistantId = 99, ModelVoice = "v" }).ShouldBeNull();
    }
}
