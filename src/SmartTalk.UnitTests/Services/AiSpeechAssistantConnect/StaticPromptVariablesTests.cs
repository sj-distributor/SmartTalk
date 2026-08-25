using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Covers the two pure helpers extracted from
/// <see cref="AiSpeechAssistantConnectService.ResolveStaticPromptVariables"/>:
/// <list type="bullet">
///   <item><c>FormatCustomerPhone</c> — strips <c>+1</c> for North American numbers,
///         null/empty-safe</item>
///   <item><c>ResolveStaticTokens</c> — replaces <c>#{user_profile}</c>,
///         <c>#{current_time}</c>, <c>#{customer_phone}</c>, <c>#{pst_date}</c> tokens
///         in the prompt; null-safe for prompt and inputs</item>
/// </list>
///
/// <para>
/// Before this PR, a null caller phone (anonymous Twilio call edge case) or a null
/// stored prompt (assistant configured without knowledge) would throw a
/// <see cref="NullReferenceException"/> that escaped the try/catch in
/// <c>ConnectAsync</c> and left the Twilio WebSocket dangling. These helpers
/// make the path null-safe.
/// </para>
/// </summary>
public class StaticPromptVariablesTests
{
    // ── FormatCustomerPhone ──────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void FormatCustomerPhone_NullOrEmpty_ReturnsEmpty(string from, string expected)
    {
        AiSpeechAssistantConnectService.FormatCustomerPhone(from).ShouldBe(expected);
    }

    [Theory]
    [InlineData("+15551234567", "5551234567")]
    [InlineData("+12025551212", "2025551212")]
    public void FormatCustomerPhone_NorthAmericanNumber_StripsPlusOne(string from, string expected)
    {
        AiSpeechAssistantConnectService.FormatCustomerPhone(from).ShouldBe(expected);
    }

    [Theory]
    [InlineData("5551234567")]                       // no leading +
    [InlineData("+8613912345678")]                   // China, must NOT strip
    [InlineData("+447911123456")]                    // UK
    [InlineData("Anonymous")]                        // Twilio anonymous marker
    [InlineData("+CALLER_ID_BLOCKED")]               // Twilio blocked-caller marker
    public void FormatCustomerPhone_NonNorthAmerican_PreservedVerbatim(string from)
    {
        AiSpeechAssistantConnectService.FormatCustomerPhone(from).ShouldBe(from);
    }

    // ── ResolveStaticTokens ──────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveStaticTokens_NullOrEmptyPrompt_ReturnsAsIs(string prompt)
    {
        var result = AiSpeechAssistantConnectService.ResolveStaticTokens(
            prompt, "{}", "+15551234567", DateTimeOffset.UtcNow);

        result.ShouldBe(prompt);
    }

    [Fact]
    public void ResolveStaticTokens_NoTokens_ReturnsPromptUnchanged()
    {
        const string prompt = "Plain prompt without placeholders.";

        var result = AiSpeechAssistantConnectService.ResolveStaticTokens(
            prompt, "{}", "+15551234567", DateTimeOffset.UtcNow);

        result.ShouldBe(prompt);
    }

    [Fact]
    public void ResolveStaticTokens_AllTokens_ReplacedWithFormattedValues()
    {
        // PST 2026-02-16 (Monday) 12:34:56 +0000 — using a fixed offset so the test
        // is deterministic regardless of host timezone.
        var pstTime = new DateTimeOffset(2026, 2, 16, 12, 34, 56, TimeSpan.FromHours(-8));
        var prompt = "User: #{user_profile}\nTime: #{current_time}\nPhone: #{customer_phone}\nDate: #{pst_date}";

        var result = AiSpeechAssistantConnectService.ResolveStaticTokens(
            prompt, "{\"name\":\"Alice\"}", "+15551234567", pstTime);

        result.ShouldBe("User: {\"name\":\"Alice\"}\nTime: 2026-02-16 12:34:56\nPhone: 5551234567\nDate: 2026-02-16 Monday");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveStaticTokens_NullOrEmptyUserProfile_ReplacesWithSpacePlaceholder(string userProfileJson)
    {
        var result = AiSpeechAssistantConnectService.ResolveStaticTokens(
            "Profile: #{user_profile}|", userProfileJson, "+15551234567", DateTimeOffset.UtcNow);

        result.ShouldBe("Profile:  |");  // single space replaces the token
    }

    [Theory]
    [InlineData(null, "")]                            // null phone → empty in token
    [InlineData("", "")]                              // empty phone → empty in token
    [InlineData("+15551234567", "5551234567")]
    [InlineData("Anonymous", "Anonymous")]
    public void ResolveStaticTokens_PhoneToken_HandledByFormatHelper(string from, string expectedPhone)
    {
        var result = AiSpeechAssistantConnectService.ResolveStaticTokens(
            "Phone[#{customer_phone}]", null, from, DateTimeOffset.UtcNow);

        result.ShouldBe($"Phone[{expectedPhone}]");
    }

    [Fact]
    public void ResolveStaticTokens_RepeatedTokens_AllOccurrencesReplaced()
    {
        var pstTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8));

        var result = AiSpeechAssistantConnectService.ResolveStaticTokens(
            "#{customer_phone} and again #{customer_phone}", null, "+15550001", pstTime);

        result.ShouldBe("5550001 and again 5550001");
    }
}
