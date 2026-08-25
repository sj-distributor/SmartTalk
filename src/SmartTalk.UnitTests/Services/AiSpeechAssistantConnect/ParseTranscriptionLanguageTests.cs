using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Exercises every input shape <see cref="AiSpeechAssistantConnectService.ParseTranscriptionLanguage"/>
/// might receive from the deserialised <c>ai_speech_assistant_function_call.content</c> JSON.
///
/// <para>
/// The single load-bearing invariant: anything that should leave the outbound
/// <c>session.audio.input.transcription</c> object byte-equivalent to the pre-hint
/// payload MUST return <c>null</c> here. A regression that returns an empty string
/// or a placeholder value would silently start sending <c>"language": ""</c> to
/// OpenAI and could break transcription quality across every prod call.
/// </para>
/// </summary>
public class ParseTranscriptionLanguageTests
{
    private static object DeserializeContent(string json) => JsonConvert.DeserializeObject<object>(json);

    // ── Default-path inputs (every one of these must return null) ──────────

    [Fact]
    public void ParseTranscriptionLanguage_NullInput_ReturnsNull()
    {
        // The realistic prod state: no row exists in ai_speech_assistant_function_call
        // for TranscriptionLanguage, DeserializeFunctionCallConfig returns null, and the
        // parser must propagate null so the adapter emits no `language` key.
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(null).ShouldBeNull();
    }

    [Fact]
    public void ParseTranscriptionLanguage_NonJObjectInput_ReturnsNull()
    {
        // Defence against future config rows whose content is a JSON array or scalar
        // (not the expected `{ "language": "..." }` shape). The parser must not throw —
        // it returns null and leaves the adapter on the default path.
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(JArray.Parse("[\"yue\"]")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage("yue").ShouldBeNull();
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(42).ShouldBeNull();
    }

    [Fact]
    public void ParseTranscriptionLanguage_EmptyJsonObject_ReturnsNull()
    {
        // A row whose content is `{}` — the language property is missing entirely.
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(DeserializeContent("{}")).ShouldBeNull();
    }

    [Fact]
    public void ParseTranscriptionLanguage_LanguagePropertyExplicitNull_ReturnsNull()
    {
        // `{ "language": null }` — operator (or migration) saved an explicit null.
        // Must behave identically to "key missing".
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(DeserializeContent("{\"language\":null}")).ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"language\":\"\"}")]
    [InlineData("{\"language\":\" \"}")]
    [InlineData("{\"language\":\"   \"}")]
    [InlineData("{\"language\":\"\\t\"}")]
    public void ParseTranscriptionLanguage_EmptyOrWhitespaceValue_ReturnsNull(string content)
    {
        // Operator-supplied empty / whitespace MUST NOT propagate to the wire payload.
        // OpenAI would reject `"language": ""` as an invalid request and kill the session.
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(DeserializeContent(content)).ShouldBeNull();
    }

    // ── Active hint inputs ─────────────────────────────────────────────────

    [Theory]
    [InlineData("{\"language\":\"yue\"}",  "yue")]    // Cantonese — non-ISO-639-1 but the BCP-47 short form
    [InlineData("{\"language\":\"zh\"}",   "zh")]     // generic Chinese (zh-CN / zh-HK / zh-TW)
    [InlineData("{\"language\":\"en\"}",   "en")]     // English
    [InlineData("{\"language\":\"es\"}",   "es")]     // Spanish
    [InlineData("{\"language\":\"ja\"}",   "ja")]     // Japanese
    [InlineData("{\"language\":\"ko\"}",   "ko")]     // Korean
    [InlineData("{\"language\":\"vi\"}",   "vi")]     // Vietnamese
    public void ParseTranscriptionLanguage_ValidValue_ReturnsTrimmedString(string content, string expected)
    {
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(DeserializeContent(content)).ShouldBe(expected);
    }

    [Fact]
    public void ParseTranscriptionLanguage_AdditionalProperties_StillExtractsLanguage()
    {
        // Forward-compatibility: future schema additions to the config row (e.g. an
        // operator note, a model variant) MUST NOT trip the parser. The single
        // `language` property is what we extract; extras are ignored.
        var input = DeserializeContent("{\"language\":\"yue\",\"note\":\"high-volume Cantonese DID\",\"model_variant\":\"v3\"}");

        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(input).ShouldBe("yue");
    }

    [Fact]
    public void ParseTranscriptionLanguage_LanguageKeyIsCaseSensitive()
    {
        // The wire-format property name is `language` (lowercase). A row with `Language`
        // or `LANGUAGE` is treated as malformed (= no hint) rather than silently activated
        // — keeps the schema interpretation strict and predictable.
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(DeserializeContent("{\"Language\":\"yue\"}")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseTranscriptionLanguage(DeserializeContent("{\"LANGUAGE\":\"yue\"}")).ShouldBeNull();
    }
}
