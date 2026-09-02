using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.BuiltIn;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class BuiltInRealtimeAiTtsProviderCompatibilityTests
{
    [Fact]
    public async Task HandleProviderAudioAsync_PreservesStringPayloadContract()
    {
        const string base64Audio = "AQID";
        var provider = new BuiltInRealtimeAiTtsProvider();
        string receivedAudio = null;
        provider.AudioChunkReadyAsync += audio =>
        {
            receivedAudio = audio;
            return Task.CompletedTask;
        };

        await provider.HandleProviderAudioAsync(base64Audio, CancellationToken.None);

        receivedAudio.ShouldBe(base64Audio);
    }
}
