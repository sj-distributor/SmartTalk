using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Covers the two pure helpers extracted from
/// <see cref="AiSpeechAssistantConnectService.ResolveDeliveryInfoAsync"/>:
/// <list type="bullet">
///   <item><c>HasDeliveryInfoToken</c> — predicate that decides whether to fetch from DB</item>
///   <item><c>ApplyDeliveryInfoTokens</c> — pure prompt token replacement</item>
/// </list>
/// Together they restore the V2 design intent that was bricked by an inverted
/// condition (<c>||</c> instead of <c>&amp;&amp;</c>) and a missing second Replace.
/// </summary>
public class DeliveryInfoTokenTests
{
    // ── HasDeliveryInfoToken ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HasDeliveryInfoToken_NullOrEmptyPrompt_ReturnsFalse(string prompt)
    {
        AiSpeechAssistantConnectService.HasDeliveryInfoToken(prompt).ShouldBeFalse();
    }

    [Fact]
    public void HasDeliveryInfoToken_PromptWithoutTokens_ReturnsFalse()
    {
        AiSpeechAssistantConnectService.HasDeliveryInfoToken("Hello, no tokens here.").ShouldBeFalse();
    }

    [Theory]
    [InlineData("Hi #{delivery_info} bye")]
    [InlineData("Hi #{DELIVERY_INFO} bye")]                          // case-insensitive
    [InlineData("Hi #{Delivery_Info} bye")]
    [InlineData("Hi #{CRM_路线_送货日数据} bye")]                       // alternate Chinese token
    [InlineData("#{delivery_info} and #{CRM_路线_送货日数据}")]         // both tokens
    public void HasDeliveryInfoToken_PromptWithEitherToken_ReturnsTrue(string prompt)
    {
        AiSpeechAssistantConnectService.HasDeliveryInfoToken(prompt).ShouldBeTrue();
    }

    // ── ApplyDeliveryInfoTokens ─────────────────────────────────

    [Theory]
    [InlineData(null, "abc", null)]
    [InlineData("", "abc", "")]
    public void ApplyDeliveryInfoTokens_NullOrEmptyPrompt_ReturnsAsIs(string prompt, string value, string expected)
    {
        AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(prompt, value).ShouldBe(expected);
    }

    [Fact]
    public void ApplyDeliveryInfoTokens_NoTokens_ReturnsPromptUnchanged()
    {
        const string prompt = "Hello world, no tokens.";

        AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(prompt, "abc").ShouldBe(prompt);
    }

    [Fact]
    public void ApplyDeliveryInfoTokens_DeliveryInfoTokenOnly_Replaced()
    {
        var result = AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(
            "Schedule: #{delivery_info}, that's it.", "Mon-Fri 9am");

        result.ShouldBe("Schedule: Mon-Fri 9am, that's it.");
    }

    [Fact]
    public void ApplyDeliveryInfoTokens_CrmDeliveryTokenOnly_Replaced()
    {
        var result = AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(
            "送货：#{CRM_路线_送货日数据}。", "周一至周五");

        result.ShouldBe("送货：周一至周五。");
    }

    [Fact]
    public void ApplyDeliveryInfoTokens_BothTokens_BothReplacedWithSameValue()
    {
        var result = AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(
            "EN: #{delivery_info}\nCN: #{CRM_路线_送货日数据}", "Wed");

        result.ShouldBe("EN: Wed\nCN: Wed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ApplyDeliveryInfoTokens_NullOrEmptyValue_ReplacesWithPlaceholderSpace(string value)
    {
        // Preserves V2's existing `?? " "` semantic for the legacy single-token path:
        // null/empty cache → placeholder " " (single space) so the prompt does not
        // contain a literal `#{...}` token visible to the model.
        var result = AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(
            "Schedule: #{delivery_info}", value);

        result.ShouldBe("Schedule:  ");  // trailing single space replaces the token
    }

    [Fact]
    public void ApplyDeliveryInfoTokens_WhitespaceOnlyValue_PreservedVerbatim()
    {
        // Whitespace-only is NOT treated as empty — caller (ResolveDeliveryInfoAsync)
        // already calls .Trim() on the cache value before passing in, so a
        // whitespace input here would only happen if the trimmed value is " "
        // (i.e. caller explicitly wants placeholder). This guards the contract that
        // the helper does not second-guess the caller's intent.
        var result = AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(
            "Schedule: #{delivery_info}", "   ");

        result.ShouldBe("Schedule:    ");  // 3 spaces (the value) replaces the token
    }

    [Fact]
    public void ApplyDeliveryInfoTokens_RepeatedTokenInPrompt_AllOccurrencesReplaced()
    {
        var result = AiSpeechAssistantConnectService.ApplyDeliveryInfoTokens(
            "#{delivery_info} and again #{delivery_info}", "X");

        result.ShouldBe("X and again X");
    }
}
