using Shouldly;
using SmartTalk.Core.Services.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderProcessJobServiceSummaryRoutingTests
{
    [Theory]
    [InlineData(4.99, true)]
    [InlineData(5, true)]
    [InlineData(5.01, false)]
    public void ShouldUseFixedInvalidSummary_ShouldApplyDurationRule(
        double durationSeconds,
        bool expected)
    {
        PhoneOrderProcessJobService
            .ShouldUseFixedInvalidSummary(durationSeconds)
            .ShouldBe(expected);
    }

    [Fact]
    public void ShouldUseFixedInvalidSummary_ShouldUseOriginalSummaryWhenDurationIsUnknown()
    {
        PhoneOrderProcessJobService
            .ShouldUseFixedInvalidSummary(null)
            .ShouldBeFalse();
    }

    [Fact]
    public void HasExactlyOneMeaningfulSpeaker_ShouldReturnTrueForOneSpeaker()
    {
        var segments = new[]
        {
            (Speaker: "speaker_0", Text: "Hello, how can I help?"),
            (Speaker: "speaker_0", Text: "Please go ahead.")
        };

        PhoneOrderProcessJobService.HasExactlyOneMeaningfulSpeaker(segments).ShouldBeTrue();
    }

    [Fact]
    public void HasExactlyOneMeaningfulSpeaker_ShouldReturnFalseForTwoSpeakers()
    {
        var segments = new[]
        {
            (Speaker: "speaker_0", Text: "Hello, how can I help?"),
            (Speaker: "speaker_1", Text: "I would like to place an order.")
        };

        PhoneOrderProcessJobService.HasExactlyOneMeaningfulSpeaker(segments).ShouldBeFalse();
    }

    [Fact]
    public void HasExactlyOneMeaningfulSpeaker_ShouldIgnorePunctuationOnlySegments()
    {
        var segments = new[]
        {
            (Speaker: "speaker_0", Text: "Hello, how can I help?"),
            (Speaker: "speaker_1", Text: "...")
        };

        PhoneOrderProcessJobService.HasExactlyOneMeaningfulSpeaker(segments).ShouldBeTrue();
    }

    [Fact]
    public void HasExactlyOneMeaningfulSpeaker_ShouldReturnFalseWhenNoSpeakerEvidenceExists()
    {
        PhoneOrderProcessJobService
            .HasExactlyOneMeaningfulSpeaker(Array.Empty<(string Speaker, string Text)>())
            .ShouldBeFalse();
    }

    [Fact]
    public void BuildInvalidConversationSummary_ShouldContainOnlyFixedInvalidCallContent()
    {
        var summary = PhoneOrderProcessJobService.BuildInvalidConversationSummary("+192552984827");

        summary.ShouldBe(
            "Conversation Topic: No valid content\n\n" +
            "- Caller ID: +192552984827\n\n" +
            "- Summary: No valid recording detected\n\n" +
            "- Guest's Emotion and Mood: Unable to determine\n\n" +
            "- Guest's Pronunciation: Unable to determine\n\n" +
            "- To-Do Items:\n\n" +
            "1. None\n\n" +
            "2. None\n\n" +
            "- Guest's Order Details: No specific order placed");
    }
}
