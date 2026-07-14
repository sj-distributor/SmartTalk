using Microsoft.Extensions.Configuration;
using Shouldly;
using SmartTalk.Core.Settings.MiniMax;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class MiniMaxTtsSettingsTests
{
    [Fact]
    public void IsEnabledForAssistant_RequiresEnabledAndMatchingAssistantId()
    {
        NewSettings(enabled: true, assistantId: "42").IsEnabledForAssistant(42).ShouldBeTrue();
        NewSettings(enabled: false, assistantId: "42").IsEnabledForAssistant(42).ShouldBeFalse();
        NewSettings(enabled: true, assistantId: "").IsEnabledForAssistant(42).ShouldBeFalse();
        NewSettings(enabled: true, assistantId: "43").IsEnabledForAssistant(42).ShouldBeFalse();
    }

    private static MiniMaxTtsSettings NewSettings(bool enabled, string assistantId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MiniMaxTts:Enabled"] = enabled.ToString(),
                ["MiniMaxTts:AssistantId"] = assistantId
            })
            .Build();

        return new MiniMaxTtsSettings(configuration);
    }
}
