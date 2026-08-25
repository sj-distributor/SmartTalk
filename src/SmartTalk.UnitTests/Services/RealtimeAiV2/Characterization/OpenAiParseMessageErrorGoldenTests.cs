using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the OpenAI adapter's RAW JSON → error-event mapping and the
/// recoverable-vs-critical classification, by feeding raw provider frames through the REAL
/// <see cref="OpenAiRealtimeAiProviderAdapter.ParseMessage"/> (not a stubbed event).
///
/// Every service-level test stubs ParseMessage to RETURN a hand-built event, so this raw mapping
/// — including the <c>last_error</c> fallback, the null-guard, the "Unknown OpenAI error" default,
/// and the IsCritical = !IsRecoverableError classification — is otherwise unpinned. Migration step
/// S3 collapses this into a normalized Kind union and S11 rebuilds the error taxonomy; both must
/// preserve this exact behavior, or these tests fail RED.
/// </summary>
public class OpenAiParseMessageErrorGoldenTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeAiErrorData ParseError(string rawJson)
    {
        var parsed = NewAdapter().ParseMessage(rawJson);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Error);
        return parsed.Data.ShouldBeOfType<RealtimeAiErrorData>();
    }

    [Theory]
    // (a) recoverable by code → not critical
    [InlineData("conversation_already_has_active_response", "busy", false)]
    // (b) recoverable by message substring (case-insensitive "active response in progress") → not critical
    [InlineData("server_error", "There is an active response in progress, please retry", false)]
    // (c) neither code nor message matches → critical
    [InlineData("server_error", "internal boom", true)]
    public void ParseMessage_ErrorEvent_ClassifiesIsCriticalFromCodeAndMessage(string code, string message, bool expectedCritical)
    {
        var raw = """{"type":"error","error":{"code":"__CODE__","message":"__MSG__"}}"""
            .Replace("__CODE__", code)
            .Replace("__MSG__", message);

        var error = ParseError(raw);

        error.Code.ShouldBe(code);
        error.Message.ShouldBe(message);
        error.IsCritical.ShouldBe(expectedCritical);
    }

    [Fact]
    public void ParseMessage_ErrorEvent_FallsBackToLastError_WhenErrorBlockAbsent()
    {
        var error = ParseError("""{"type":"error","last_error":{"code":"conversation_already_has_active_response","message":"resuming"}}""");

        error.Code.ShouldBe("conversation_already_has_active_response");
        error.Message.ShouldBe("resuming");
        error.IsCritical.ShouldBeFalse();
    }

    [Fact]
    public void ParseMessage_ErrorEvent_NullLastErrorGuarded_DefaultsToUnknownCritical()
    {
        var error = ParseError("""{"type":"error","last_error":null}""");

        error.Code.ShouldBeNull();
        error.Message.ShouldBe("Unknown OpenAI error");
        error.IsCritical.ShouldBeTrue();
    }

    [Fact]
    public void ParseMessage_ErrorEvent_BothAbsent_DefaultsToUnknownCritical()
    {
        var error = ParseError("""{"type":"error"}""");

        error.Code.ShouldBeNull();
        error.Message.ShouldBe("Unknown OpenAI error");
        error.IsCritical.ShouldBeTrue();
    }
}
