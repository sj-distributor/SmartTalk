using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Exercises every input shape <see cref="AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens"/>
/// might receive from the deserialised <c>ai_speech_assistant_function_call.content</c> JSON.
///
/// <para>
/// The single load-bearing invariant: anything that should leave the response cap
/// unset (so OpenAI uses its server-side default) MUST return <c>null</c> here.
/// In particular, a non-positive integer (zero or negative) — which OpenAI rejects
/// or which would silently truncate AI responses to empty turns — is rejected by
/// the parser rather than passed through.
/// </para>
///
/// <para>
/// Mirrors the structure of <see cref="ParseTranscriptionLanguageTests"/> and
/// <see cref="ParseTranscriptionModelTests"/>; the three parsers share the same
/// null-safety contract so regressions are visible in all three suites together.
/// </para>
/// </summary>
public class ParseMaxResponseOutputTokensTests
{
    private static object DeserializeContent(string json) => JsonConvert.DeserializeObject<object>(json);

    // ── Default-path inputs (every one of these must return null) ──────────

    [Fact]
    public void ParseMaxResponseOutputTokens_NullInput_ReturnsNull()
    {
        // Realistic prod state: no MaxResponseOutputTokens row exists, so
        // DeserializeFunctionCallConfig returns null. The parser must propagate
        // null so the adapter omits the cap field entirely.
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(null).ShouldBeNull();
    }

    [Fact]
    public void ParseMaxResponseOutputTokens_NonJObjectInput_ReturnsNull()
    {
        // Defence: future config rows whose content is a JSON array or scalar
        // (not the expected `{ "value": <int> }` shape) must not throw.
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(JArray.Parse("[1200]")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(1200).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens("1200").ShouldBeNull();
    }

    [Fact]
    public void ParseMaxResponseOutputTokens_EmptyJsonObject_ReturnsNull()
    {
        // A row whose content is `{}` — the value property is missing entirely.
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(DeserializeContent("{}")).ShouldBeNull();
    }

    [Fact]
    public void ParseMaxResponseOutputTokens_ValuePropertyExplicitNull_ReturnsNull()
    {
        // `{ "value": null }` — operator (or migration) saved an explicit null.
        // Must behave identically to "key missing".
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(DeserializeContent("{\"value\":null}")).ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"value\":\"1200\"}")]   // string instead of int — schema violation
    [InlineData("{\"value\":1.5}")]        // floating-point — schema violation
    [InlineData("{\"value\":true}")]       // bool — schema violation
    [InlineData("{\"value\":[1200]}")]     // array — schema violation
    public void ParseMaxResponseOutputTokens_NonIntegerValue_ReturnsNull(string content)
    {
        // The JSON schema requires the value to be a plain integer. Anything else
        // (string-coercible numbers, floats, bools, structured values) is treated
        // as malformed — adapter falls back to no cap, which is safer than
        // attempting a fragile coercion that could send an invalid value to OpenAI.
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(DeserializeContent(content)).ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"value\":0}")]
    [InlineData("{\"value\":-1}")]
    [InlineData("{\"value\":-1000}")]
    public void ParseMaxResponseOutputTokens_NonPositiveValue_ReturnsNull(string content)
    {
        // Zero or negative caps are either rejected by OpenAI server-side (invalid)
        // or silently truncate AI responses to empty turns (one), both worse than
        // omitting the cap. Reject at the parser layer.
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(DeserializeContent(content)).ShouldBeNull();
    }

    // ── Active cap inputs ─────────────────────────────────────────────────

    [Theory]
    [InlineData("{\"value\":1}",     1)]      // minimum valid (degenerate but technically valid)
    [InlineData("{\"value\":100}",   100)]
    [InlineData("{\"value\":800}",   800)]    // recommended low end for restaurant turns
    [InlineData("{\"value\":1200}",  1200)]   // recommended typical cap
    [InlineData("{\"value\":4096}",  4096)]   // common upper bound
    [InlineData("{\"value\":100000}", 100000)] // very generous cap — operator's choice
    public void ParseMaxResponseOutputTokens_PositiveIntegerValue_ReturnsExactInteger(string content, int expected)
    {
        // No upper bound is enforced — operators choose the cap they want, and
        // OpenAI rejects values beyond its server-side limits with a clear error
        // rather than us silently clamping.
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(DeserializeContent(content)).ShouldBe(expected);
    }

    [Fact]
    public void ParseMaxResponseOutputTokens_AdditionalProperties_StillExtractsValue()
    {
        // Forward-compatibility: future schema additions (e.g. an operator note,
        // a per-turn override) must not trip the parser.
        var input = DeserializeContent("{\"value\":1200,\"note\":\"cap monologues\",\"applies_to\":\"all_turns\"}");

        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(input).ShouldBe(1200);
    }

    [Fact]
    public void ParseMaxResponseOutputTokens_ValueKeyIsCaseSensitive()
    {
        // Wire-format property name is `value` (lowercase). A row with `Value` or
        // `VALUE` is treated as malformed (= no cap) rather than silently activated.
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(DeserializeContent("{\"Value\":1200}")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseMaxResponseOutputTokens(DeserializeContent("{\"VALUE\":1200}")).ShouldBeNull();
    }
}
