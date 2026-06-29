using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.Google;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.Google;
using SmartTalk.Core.Settings.OpenAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// Pins the inference adapters' declared <c>Capabilities</c> (S2). The engine's output-mode negotiator
/// (S4) reads these instead of branching on the provider enum, so OpenAI must declare text-only output
/// (to drive external TTS) while the current Google adapter must declare no text output (its
/// modelTurn-text arm is unfinished) so a capability gate never pairs it with a text-driven TTS.
/// </summary>
public class InferenceAdapterCapabilitiesGoldenTests
{
    [Fact]
    public void OpenAi_Declares_TextOnly_AndAudio()
    {
        var caps = new OpenAiRealtimeAiProviderAdapter(new OpenAiSettings(Substitute.For<IConfiguration>())).Capabilities;

        caps.TextOutput.CanEmitTextOnly.ShouldBeTrue();
        caps.TextOutput.CanEmitTextAlongsideAudio.ShouldBeFalse();
        caps.SupportsAudioOutput.ShouldBeTrue();
    }

    [Fact]
    public void Google_Declares_NoTextOutput_AudioOnly()
    {
        var caps = new GoogleRealtimeAiProviderAdapter(new GoogleSettings(Substitute.For<IConfiguration>())).Capabilities;

        caps.TextOutput.CanEmitTextOnly.ShouldBeFalse();
        caps.TextOutput.CanEmitTextAlongsideAudio.ShouldBeFalse();
        caps.SupportsAudioOutput.ShouldBeTrue();
    }
}
