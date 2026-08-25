using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Exercises every input shape <see cref="AiSpeechAssistantConnectService.ParseTranscriptionModel"/>
/// might receive from the deserialised <c>ai_speech_assistant_function_call.content</c> JSON.
///
/// <para>
/// The single load-bearing invariant: anything that should leave the adapter on its
/// compile-time default model MUST return <c>null</c> here. A regression that returns
/// an empty string or a placeholder value would silently send <c>"model": ""</c> (or
/// worse, an unintended downgrade) to OpenAI, breaking transcription quality across
/// affected calls.
/// </para>
///
/// <para>
/// Mirrors the structure of <see cref="ParseTranscriptionLanguageTests"/>; the two
/// parsers share the same null-safety contract so regressions are visible in both
/// suites together.
/// </para>
/// </summary>
public class ParseTranscriptionModelTests
{
    private static object DeserializeContent(string json) => JsonConvert.DeserializeObject<object>(json);

    // ── Default-path inputs (every one of these must return null) ──────────

    [Fact]
    public void ParseTranscriptionModel_NullInput_ReturnsNull()
    {
        // Realistic prod state: no TranscriptionModel row exists, so
        // DeserializeFunctionCallConfig returns null. The parser must propagate
        // null so the adapter stays on its compile-time default.
        AiSpeechAssistantConnectService.ParseTranscriptionModel(null).ShouldBeNull();
    }

    [Fact]
    public void ParseTranscriptionModel_NonJObjectInput_ReturnsNull()
    {
        // Defence: future config rows whose content is a JSON array or scalar
        // (not the expected `{ "model": "..." }` shape) must not throw.
        AiSpeechAssistantConnectService.ParseTranscriptionModel(JArray.Parse("[\"whisper-1\"]")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseTranscriptionModel("whisper-1").ShouldBeNull();
        AiSpeechAssistantConnectService.ParseTranscriptionModel(42).ShouldBeNull();
    }

    [Fact]
    public void ParseTranscriptionModel_EmptyJsonObject_ReturnsNull()
    {
        // A row whose content is `{}` — the model property is missing entirely.
        AiSpeechAssistantConnectService.ParseTranscriptionModel(DeserializeContent("{}")).ShouldBeNull();
    }

    [Fact]
    public void ParseTranscriptionModel_ModelPropertyExplicitNull_ReturnsNull()
    {
        // `{ "model": null }` — operator (or migration) saved an explicit null.
        // Must behave identically to "key missing".
        AiSpeechAssistantConnectService.ParseTranscriptionModel(DeserializeContent("{\"model\":null}")).ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"model\":\"\"}")]
    [InlineData("{\"model\":\" \"}")]
    [InlineData("{\"model\":\"   \"}")]
    [InlineData("{\"model\":\"\\t\"}")]
    public void ParseTranscriptionModel_EmptyOrWhitespaceValue_ReturnsNull(string content)
    {
        // Operator-supplied empty / whitespace MUST NOT propagate to the wire payload.
        // OpenAI would reject `"model": ""` as an invalid request and kill the session.
        AiSpeechAssistantConnectService.ParseTranscriptionModel(DeserializeContent(content)).ShouldBeNull();
    }

    // ── Active override inputs ─────────────────────────────────────────────

    [Theory]
    [InlineData("{\"model\":\"whisper-1\"}",               "whisper-1")]                // legacy downgrade
    [InlineData("{\"model\":\"gpt-4o-mini-transcribe\"}",  "gpt-4o-mini-transcribe")]   // cheaper variant
    [InlineData("{\"model\":\"gpt-4o-transcribe\"}",       "gpt-4o-transcribe")]        // explicit re-affirm of default
    [InlineData("{\"model\":\"gpt-5-transcribe\"}",        "gpt-5-transcribe")]         // forward-compat: future model passthrough
    public void ParseTranscriptionModel_RecognisedAndFuturePassthrough_ReturnsExactString(string content, string expected)
    {
        // The parser does NOT validate the string against a known list. Operators
        // can opt into future OpenAI models without a code change, and OpenAI
        // rejects unknown values server-side rather than us silently substituting.
        AiSpeechAssistantConnectService.ParseTranscriptionModel(DeserializeContent(content)).ShouldBe(expected);
    }

    [Fact]
    public void ParseTranscriptionModel_AdditionalProperties_StillExtractsModel()
    {
        // Forward-compatibility: future schema additions (e.g. an operator note,
        // a temperature override) must not trip the parser.
        var input = DeserializeContent("{\"model\":\"whisper-1\",\"note\":\"cost-sensitive DID\",\"variant\":\"v1\"}");

        AiSpeechAssistantConnectService.ParseTranscriptionModel(input).ShouldBe("whisper-1");
    }

    [Fact]
    public void ParseTranscriptionModel_ModelKeyIsCaseSensitive()
    {
        // Wire-format property name is `model` (lowercase). A row with `Model` or
        // `MODEL` is treated as malformed (= no override) rather than silently
        // activated — keeps schema interpretation strict and predictable.
        AiSpeechAssistantConnectService.ParseTranscriptionModel(DeserializeContent("{\"Model\":\"whisper-1\"}")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseTranscriptionModel(DeserializeContent("{\"MODEL\":\"whisper-1\"}")).ShouldBeNull();
    }
}
