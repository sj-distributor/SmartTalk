using System.Text;
using System.Text.Json;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Covers per-call prompt-variable decoding, validation, merging and single-pass rendering.
/// </summary>
public class AiSpeechAssistantPromptVariablesTests
{
    [Fact]
    public void TryResolvePromptVariables_Base64UrlJsonWithMultipleVariables_ResolvesUnicodeAndNewLines()
    {
        var encoded = EncodeJson(new Dictionary<string, string>
        {
            ["question"] = "请问今天几点关门？\n四位需要等位吗？",
            ["merchant_name"] = "海风餐厅"
        });

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            encoded, null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved.Count.ShouldBe(2);
        resolved["question"].ShouldBe("请问今天几点关门？\n四位需要等位吗？");
        resolved["merchant_name"].ShouldBe("海风餐厅");
    }

    [Fact]
    public void TryResolvePromptVariables_SuppliedVariablesOverrideEncodedVariables_CaseInsensitively()
    {
        var encoded = EncodeJson(new Dictionary<string, string>
        {
            ["question"] = "encoded question",
            ["merchant_name"] = "encoded merchant"
        });
        IReadOnlyDictionary<string, string> supplied = new Dictionary<string, string>
        {
            ["QUESTION"] = "supplied question",
            ["task_note"] = "supplied note"
        };

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            encoded, supplied, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved.Count.ShouldBe(3);
        resolved["question"].ShouldBe("supplied question");
        resolved["merchant_name"].ShouldBe("encoded merchant");
        resolved["task_note"].ShouldBe("supplied note");
    }

    [Fact]
    public void TryResolvePromptVariables_LegacyRawQuestion_MapsToQuestionVariable()
    {
        var encoded = EncodeText("What time do you close today?\nIs there a wait for four people?");

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            null, encoded, null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved.Count.ShouldBe(1);
        resolved["question"].ShouldBe("What time do you close today?\nIs there a wait for four people?");
    }

    [Fact]
    public void TryResolvePromptVariables_LegacyQuestionStartingWithJsonCharacter_RemainsPlainText()
    {
        var encoded = EncodeText("{Please ask whether the restaurant is open}");

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            null, encoded, null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved["question"].ShouldBe("{Please ask whether the restaurant is open}");
    }

    [Fact]
    public void TryResolvePromptVariables_LegacyQuestionOutsideGenericLimits_RemainsCompatible()
    {
        var question = new string('a', 4097);

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            null, EncodeText(question), null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved["question"].ShouldBe(question);
    }

    [Fact]
    public void TryResolvePromptVariables_LegacyQuestionWithInvalidUtf8_UsesReplacementFallback()
    {
        byte[] questionBytes = [0xC3, 0x28];

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            null, EncodeBytes(questionBytes), null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved["question"].ShouldBe(Encoding.UTF8.GetString(questionBytes));
    }

    [Fact]
    public void TryResolvePromptVariables_GenericPayloadMustBeJsonObject()
    {
        AssertResolveFails(EncodeText("plain text belongs to the legacy question route"));
    }

    [Fact]
    public void TryResolvePromptVariables_GenericAndLegacyPayloadTogether_ReturnsError()
    {
        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            EncodeJson(new Dictionary<string, string> { ["question"] = "generic" }),
            EncodeText("legacy"), null, out var resolved, out var error);

        success.ShouldBeFalse();
        resolved.ShouldBeEmpty();
        string.IsNullOrWhiteSpace(error).ShouldBeFalse();
    }

    [Fact]
    public void TryResolvePromptVariables_NoEncodedOrSuppliedVariables_ReturnsEmptyVariables()
    {
        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            null, null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved.ShouldBeEmpty();
    }

    [Fact]
    public void TryResolvePromptVariables_JsonNullValue_NormalizesToEmptyString()
    {
        var encoded = EncodeText("{\"question\":null}");

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            encoded, null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved["question"].ShouldBe(string.Empty);
    }

    [Fact]
    public void TryResolvePromptVariables_SuppliedNullValue_NormalizesToEmptyString()
    {
        IReadOnlyDictionary<string, string> supplied = new Dictionary<string, string>
        {
            ["question"] = null
        };

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            null, supplied, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved["question"].ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("%%%")]
    [InlineData("not_base64url!")]
    public void TryResolvePromptVariables_InvalidBase64Url_ReturnsError(string encoded)
    {
        AssertResolveFails(encoded);
    }

    [Fact]
    public void TryResolvePromptVariables_InvalidUtf8_ReturnsError()
    {
        AssertResolveFails(EncodeBytes([0xC3, 0x28]));
    }

    [Theory]
    [InlineData("{\"question\":")]
    [InlineData("{not-json}")]
    public void TryResolvePromptVariables_InvalidJsonObject_ReturnsError(string json)
    {
        AssertResolveFails(EncodeText(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("question name")]
    [InlineData("question/value")]
    public void TryResolvePromptVariables_InvalidKey_ReturnsError(string key)
    {
        AssertResolveFails(EncodeJson(new Dictionary<string, string> { [key] = "value" }));
    }

    [Fact]
    public void TryResolvePromptVariables_IdentifierCharactersAtKeyStart_ResolveSuccessfully()
    {
        var encoded = EncodeJson(new Dictionary<string, string>
        {
            ["1reference"] = "numeric",
            ["_context"] = "underscore",
            [".metadata"] = "dot",
            ["-locale"] = "hyphen"
        });

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            encoded, null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved["1reference"].ShouldBe("numeric");
        resolved["_context"].ShouldBe("underscore");
        resolved[".metadata"].ShouldBe("dot");
        resolved["-locale"].ShouldBe("hyphen");
    }

    [Fact]
    public void TryResolvePromptVariables_KeyLongerThan64Characters_ReturnsError()
    {
        var key = "a" + new string('b', 64);

        AssertResolveFails(EncodeJson(new Dictionary<string, string> { [key] = "value" }));
    }

    [Theory]
    [InlineData("{\"question\":123}")]
    [InlineData("{\"question\":true}")]
    [InlineData("{\"question\":[]}")]
    [InlineData("{\"question\":{}}")]
    public void TryResolvePromptVariables_NonStringValue_ReturnsError(string json)
    {
        AssertResolveFails(EncodeText(json));
    }

    [Fact]
    public void TryResolvePromptVariables_EncodedPayloadLongerThan4096Characters_ReturnsError()
    {
        AssertResolveFails(new string('A', 4097));
    }

    [Fact]
    public void TryResolvePromptVariables_MoreThan20Variables_ReturnsError()
    {
        var variables = Enumerable.Range(1, 21)
            .ToDictionary(index => $"variable_{index}", index => index.ToString());

        AssertResolveFails(EncodeJson(variables));
    }

    [Fact]
    public void TryResolvePromptVariables_ValueLongerThan2048Characters_ReturnsError()
    {
        var variables = new Dictionary<string, string>
        {
            ["question"] = new string('a', 2049)
        };

        AssertResolveFails(EncodeJson(variables));
    }

    [Fact]
    public void TryResolvePromptVariables_ValuesAtValidationBoundaries_ResolveSuccessfully()
    {
        var variables = Enumerable.Range(1, 20)
            .ToDictionary(index => $"variable_{index}", index => index == 20 ? new string('a', 2048) : index.ToString());
        var longestValidKey = "a" + new string('b', 63);
        variables.Remove("variable_1");
        variables[longestValidKey] = "valid";

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            EncodeJson(variables), null, out var resolved, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        resolved.Count.ShouldBe(20);
        resolved[longestValidKey].ShouldBe("valid");
        resolved["variable_20"].Length.ShouldBe(2048);
    }

    [Fact]
    public void TryResolvePromptVariables_InvalidSuppliedVariable_ReturnsError()
    {
        IReadOnlyDictionary<string, string> supplied = new Dictionary<string, string>
        {
            ["invalid key"] = "value"
        };

        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            null, supplied, out var resolved, out var error);

        success.ShouldBeFalse();
        resolved.ShouldBeEmpty();
        string.IsNullOrWhiteSpace(error).ShouldBeFalse();
    }

    [Fact]
    public void ResolvePromptVariables_MatchesKeysCaseInsensitivelyAndReplacesEveryOccurrence()
    {
        IReadOnlyDictionary<string, string> variables = new Dictionary<string, string>
        {
            ["question"] = "What time do you close?"
        };

        var result = AiSpeechAssistantService.ResolvePromptVariables(
            "First: #{QUESTION}\nSecond: #{Question}", variables);

        result.ShouldBe("First: What time do you close?\nSecond: What time do you close?");
    }

    [Fact]
    public void ResolvePromptVariables_MissingVariable_PreservesPlaceholder()
    {
        IReadOnlyDictionary<string, string> variables = new Dictionary<string, string>
        {
            ["known"] = "resolved"
        };

        var result = AiSpeechAssistantService.ResolvePromptVariables(
            "#{known}|#{missing}", variables);

        result.ShouldBe("resolved|#{missing}");
    }

    [Fact]
    public void ResolvePromptVariables_MissingLegacyQuestion_ClearsPlaceholder()
    {
        var result = AiSpeechAssistantService.ResolvePromptVariables(
            "Question: #{question}", new Dictionary<string, string>());

        result.ShouldBe("Question: ");
    }

    [Fact]
    public void ResolvePromptVariables_ReplacementValueContainingPlaceholder_DoesNotCascade()
    {
        IReadOnlyDictionary<string, string> variables = new Dictionary<string, string>
        {
            ["first"] = "#{second}",
            ["second"] = "final value"
        };

        var result = AiSpeechAssistantService.ResolvePromptVariables("#{first}", variables);

        result.ShouldBe("#{second}");
    }

    [Fact]
    public void ResolvePromptVariables_ReplacementValueWithRegexCharacters_IsInsertedVerbatim()
    {
        IReadOnlyDictionary<string, string> variables = new Dictionary<string, string>
        {
            ["value"] = "$1 \\ #{unchanged}"
        };

        var result = AiSpeechAssistantService.ResolvePromptVariables("Value: #{value}", variables);

        result.ShouldBe("Value: $1 \\ #{unchanged}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolvePromptVariables_NullOrEmptyPrompt_ReturnsAsIs(string prompt)
    {
        var variables = new Dictionary<string, string> { ["question"] = "value" };

        AiSpeechAssistantService.ResolvePromptVariables(prompt, variables).ShouldBe(prompt);
    }

    [Fact]
    public void ResolvePromptVariables_NullVariables_ReturnsPromptUnchanged()
    {
        const string prompt = "Merchant: #{merchant_name}";

        AiSpeechAssistantService.ResolvePromptVariables(prompt, null).ShouldBe(prompt);
    }

    private static void AssertResolveFails(string encoded)
    {
        var success = AiSpeechAssistantService.TryResolvePromptVariables(
            encoded, null, out var resolved, out var error);

        success.ShouldBeFalse();
        resolved.ShouldBeEmpty();
        string.IsNullOrWhiteSpace(error).ShouldBeFalse();
    }

    private static string EncodeJson(IReadOnlyDictionary<string, string> variables)
    {
        return EncodeText(JsonSerializer.Serialize(variables));
    }

    private static string EncodeText(string value)
    {
        return EncodeBytes(Encoding.UTF8.GetBytes(value));
    }

    private static string EncodeBytes(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
