using Shouldly;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Core.Services.AiSpeechAssistantConnect.Exceptions;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Covers the V2 null-defense extracted from
/// <see cref="AiSpeechAssistantConnectService.LoadAssistantInfoAsync"/>.
///
/// <para>
/// Before this PR, an unmatched (caller, DID, assistantId) tuple let the data
/// provider's <c>FirstOrDefaultAsync(...)</c> return null, then the
/// <c>return (result.assistant, ...)</c> dereference threw NullReferenceException
/// from inside the data provider — an exception type that escaped the V2 ConnectAsync
/// try/catch (which only handled AiAssistantNotAvailableException and AiAssistantCallForwardedException),
/// leaking up the call stack and leaving the Twilio WebSocket dangling.
/// </para>
///
/// <para>
/// The fix: data provider returns a tuple of nulls (via <c>?.</c>); V2 explicitly
/// checks for null assistant or null knowledge and throws AiAssistantNotAvailableException,
/// which the existing try/catch handles cleanly.
/// </para>
/// </summary>
public class EnsureAssistantInfoCompleteTests
{
    [Fact]
    public void EnsureAssistantInfoComplete_BothNonNull_ReturnsWithoutThrowing()
    {
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant { Id = 42 };
        var knowledge = new AiSpeechAssistantKnowledge { AssistantId = 42, Prompt = "p" };

        Should.NotThrow(() =>
            AiSpeechAssistantConnectService.EnsureAssistantInfoComplete(assistant, knowledge));
    }

    [Fact]
    public void EnsureAssistantInfoComplete_NullAssistant_ThrowsNotAvailable()
    {
        var ex = Should.Throw<AiAssistantNotAvailableException>(() =>
            AiSpeechAssistantConnectService.EnsureAssistantInfoComplete(null, null));

        ex.Message.ShouldContain("No assistant configured");
    }

    [Fact]
    public void EnsureAssistantInfoComplete_NullKnowledge_ThrowsNotAvailable_WithAssistantId()
    {
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant { Id = 99 };

        var ex = Should.Throw<AiAssistantNotAvailableException>(() =>
            AiSpeechAssistantConnectService.EnsureAssistantInfoComplete(assistant, null));

        ex.Message.ShouldContain("No active knowledge");
        ex.Message.ShouldContain("99");  // includes assistant id for diagnostics
    }

    [Fact]
    public void EnsureAssistantInfoComplete_NullAssistantTakesPrecedenceOverNullKnowledge()
    {
        // When both are null, the assistant-missing message wins (more actionable).
        var ex = Should.Throw<AiAssistantNotAvailableException>(() =>
            AiSpeechAssistantConnectService.EnsureAssistantInfoComplete(null, null));

        ex.Message.ShouldContain("No assistant configured");
        ex.Message.ShouldNotContain("No active knowledge");
    }
}
