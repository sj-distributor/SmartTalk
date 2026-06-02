using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Exercises every input shape <see cref="AiSpeechAssistantConnectService.ParseOutputAudioSpeed"/>
/// might receive from the deserialised <c>ai_speech_assistant_function_call.content</c> JSON.
///
/// <para>
/// The single load-bearing invariant: anything that should leave the speed
/// unset (so OpenAI uses 1.0, current behaviour) MUST return <c>null</c>.
/// Non-positive values are rejected at the parser layer because zero / negative
/// playback speed is meaningless to OpenAI and would either be rejected
/// server-side or worse interpreted differently across model versions.
/// </para>
///
/// <para>
/// The parser does NOT enforce OpenAI's documented range (currently 0.25 – 1.5).
/// Operators who set out-of-range values are rejected by OpenAI server-side
/// with a clear error rather than the adapter silently clamping into a
/// different speed than the operator intended.
/// </para>
/// </summary>
public class ParseOutputAudioSpeedTests
{
    private static object DeserializeContent(string json) => JsonConvert.DeserializeObject<object>(json);

    // ── Default-path inputs (every one of these must return null) ──────────

    [Fact]
    public void ParseOutputAudioSpeed_NullInput_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(null).ShouldBeNull();
    }

    [Fact]
    public void ParseOutputAudioSpeed_NonJObjectInput_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(JArray.Parse("[1.0]")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(1.0).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed("1.0").ShouldBeNull();
    }

    [Fact]
    public void ParseOutputAudioSpeed_EmptyJsonObject_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent("{}")).ShouldBeNull();
    }

    [Fact]
    public void ParseOutputAudioSpeed_ValuePropertyExplicitNull_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent("{\"value\":null}")).ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"value\":\"1.0\"}")]   // string instead of number
    [InlineData("{\"value\":true}")]      // bool
    [InlineData("{\"value\":[1.0]}")]     // array
    public void ParseOutputAudioSpeed_NonNumericValue_ReturnsNull(string content)
    {
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent(content)).ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"value\":0}")]
    [InlineData("{\"value\":0.0}")]
    [InlineData("{\"value\":-0.5}")]
    [InlineData("{\"value\":-1.0}")]
    public void ParseOutputAudioSpeed_NonPositiveValue_ReturnsNull(string content)
    {
        // Zero or negative playback speed is meaningless; reject at the parser
        // rather than emit a value that OpenAI either rejects or interprets
        // inconsistently across model versions.
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent(content)).ShouldBeNull();
    }

    // ── Active speed inputs ────────────────────────────────────────────────

    [Theory]
    [InlineData("{\"value\":0.25}", "0.25")]    // documented OpenAI minimum
    [InlineData("{\"value\":0.8}",  "0.8")]     // a slower-than-natural setting
    [InlineData("{\"value\":0.9}",  "0.9")]     // recommended for elderly customers
    [InlineData("{\"value\":1.0}",  "1.0")]     // explicit natural speed
    [InlineData("{\"value\":1.1}",  "1.1")]     // recommended for fast-paced customers
    [InlineData("{\"value\":1.5}",  "1.5")]     // documented OpenAI maximum
    public void ParseOutputAudioSpeed_PositiveDecimal_ReturnsExactValue(string content, string expectedString)
    {
        // Use string literals to avoid C# decimal-literal precision surprises in the
        // [InlineData] table; convert here so each case is self-documenting.
        var expected = decimal.Parse(expectedString, System.Globalization.CultureInfo.InvariantCulture);

        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent(content)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("{\"value\":2}",   2)]      // integer 2 — passed through (out of OpenAI's 1.5 max but accepted by parser)
    [InlineData("{\"value\":100}", 100)]    // way out of range — rejected by OpenAI, not the parser
    public void ParseOutputAudioSpeed_IntegerValue_AcceptedAsDecimal(string content, int expectedInt)
    {
        // Integers also count as positive numerics. The parser does not enforce
        // OpenAI's documented 0.25 – 1.5 range — out-of-range values are passed
        // through and rejected by OpenAI server-side with a clear error.
        var expected = (decimal)expectedInt;

        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent(content)).ShouldBe(expected);
    }

    [Fact]
    public void ParseOutputAudioSpeed_AdditionalProperties_StillExtractsValue()
    {
        var input = DeserializeContent("{\"value\":0.9,\"note\":\"elderly assistant\"}");

        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(input).ShouldBe(0.9m);
    }

    [Fact]
    public void ParseOutputAudioSpeed_ValueKeyIsCaseSensitive()
    {
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent("{\"Value\":0.9}")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseOutputAudioSpeed(DeserializeContent("{\"VALUE\":0.9}")).ShouldBeNull();
    }
}
