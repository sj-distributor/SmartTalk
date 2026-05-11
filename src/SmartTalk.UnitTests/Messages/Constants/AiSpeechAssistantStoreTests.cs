using Shouldly;
using SmartTalk.Messages.Constants;
using Xunit;

namespace SmartTalk.UnitTests.Messages.Constants;

/// <summary>
/// Pins the literal Realtime API URL strings advertised by <see cref="AiSpeechAssistantStore"/>.
///
/// <para>
/// These URLs flow into the OpenAI Realtime WebSocket connection any time an
/// <c>AiSpeechAssistant</c> row has no <c>ModelUrl</c> override (the typical case).
/// A silent change here re-points every assistant in production at a different
/// OpenAI model; pinning the literal makes such a change a CI-visible decision.
/// </para>
/// </summary>
public class AiSpeechAssistantStoreTests
{
    private const string OpenAiRealtimeWssPrefix = "wss://api.openai.com/v1/realtime?model=";

    [Fact]
    public void DefaultUrl_PointsAtGptRealtime2()
    {
        // gpt-realtime-2 is the GA realtime model (released 2026-05-07), superseding
        // gpt-realtime-1.5. Same pricing, GPT-5-class reasoning, 128K context.
        AiSpeechAssistantStore.DefaultUrl
            .ShouldBe("wss://api.openai.com/v1/realtime?model=gpt-realtime-2");
    }

    [Fact]
    public void AiKidDefaultUrl_PointsAtGpt4oMiniRealtimePreview()
    {
        AiSpeechAssistantStore.AiKidDefaultUrl
            .ShouldBe("wss://api.openai.com/v1/realtime?model=gpt-4o-mini-realtime-preview-2024-12-17");
    }

    [Theory]
    [InlineData(nameof(AiSpeechAssistantStore.DefaultUrl))]
    [InlineData(nameof(AiSpeechAssistantStore.AiKidDefaultUrl))]
    public void AllAdvertisedUrls_StartWithOpenAiRealtimeWssPrefix(string fieldName)
    {
        var value = typeof(AiSpeechAssistantStore)
            .GetField(fieldName)!
            .GetValue(null)!
            .ToString()!;

        value.ShouldStartWith(OpenAiRealtimeWssPrefix,
            customMessage: $"{fieldName} must point at OpenAI Realtime WebSocket endpoint");
        value.Length.ShouldBeGreaterThan(OpenAiRealtimeWssPrefix.Length,
            customMessage: $"{fieldName} must include a model id after the prefix");
    }
}
