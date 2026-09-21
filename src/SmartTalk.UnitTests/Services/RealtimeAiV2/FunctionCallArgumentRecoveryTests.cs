using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Three tool handlers deserialized the model's arguments with no guard, so a malformed payload threw,
/// the engine discarded that tool's reply, and — when it was the only call in the batch — nothing
/// triggered a response. The caller finished stating their order and heard nothing at all until the
/// 60-second idle follow-up hung them up.
///
/// <para>The recovery reply that fixes that also removes the only thing ending such a call. Today's
/// discarded reply guarantees exactly one turn completes, which is what lets the idle timer run to
/// term — and <c>InactivityTimerManager.StartTimer</c> opens by stopping the timer, so every turn
/// completion restarts the 60-second countdown from zero. Reply unconditionally and a model that
/// re-emits the same unparseable arguments loops at realtime speed, the countdown never reaches 60,
/// and the production phone path has no session ceiling to catch it. That is a call that never ends.
/// </para>
///
/// <para>So the recovery is claimed once per tool per call. The second failure falls back to today's
/// behaviour and the hangup happens as it does now. These pin that boundary.</para>
/// </summary>
public class FunctionCallArgumentRecoveryTests
{
    // ── the parse guard ────────────────────────────────────────────

    [Theory]
    [InlineData(null)]                                                       // the adapter yields null when 'arguments' is absent
    [InlineData("{\"order_items\":[{\"item_n")]                              // a truncated stream
    [InlineData("{\"order_items\":[{\"item_name\":\"Tea\",\"price\":null}]}")] // null into a non-nullable decimal
    public void UnreadableArguments_ShouldReportFailureRatherThanThrow(string argumentsJson)
    {
        var succeeded = Should.NotThrow(() => AiSpeechAssistantConnectService.TryParseFunctionCallArguments<AiSpeechAssistantOrderDto>(
            argumentsJson, "order", "CA_test", out _));

        succeeded.ShouldBeFalse();
    }

    [Fact]
    public void UnreadableArguments_ShouldLeaveTheParsedValueNull()
    {
        // The caller must therefore skip its assignment. Clobbering an order already confirmed earlier
        // in the call with a half-parsed one would be worse than the dead air this fixes.
        AiSpeechAssistantConnectService.TryParseFunctionCallArguments<AiSpeechAssistantOrderDto>("{bad", "order", "CA_test", out var parsed);

        parsed.ShouldBeNull();
    }

    [Fact]
    public void ReadableArguments_ShouldParseExactlyAsBefore()
    {
        AiSpeechAssistantConnectService.TryParseFunctionCallArguments<AiSpeechAssistantOrderDto>(
            """{"order_items":[{"item_name":"Pad Thai","quantity":1,"price":12.5}]}""", "order", "CA_test", out var parsed)
            .ShouldBeTrue();

        parsed.ShouldNotBeNull();
        parsed.Order.Count.ShouldBe(1);
        parsed.Order[0].Name.ShouldBe("Pad Thai");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    public void ArgumentsThatDeserializeToNullWithoutThrowing_ShouldStillCountAsSuccess(string argumentsJson)
    {
        // Scoping this honestly: these never threw, so they take the success path and assign null today
        // exactly as they did before. The guard covers the throwing subset, not "unreadable" in general.
        AiSpeechAssistantConnectService.TryParseFunctionCallArguments<AiSpeechAssistantOrderDto>(
            argumentsJson, "order", "CA_test", out var parsed).ShouldBeTrue();

        parsed.ShouldBeNull();
    }

    // ── the one-shot recovery claim ────────────────────────────────

    [Fact]
    public void ASecondFailureForTheSameTool_ShouldNotClaimAnotherRecovery()
    {
        // THE test. One recovery attempt instead of instant dead air; after that, today's behaviour and
        // today's deterministic hangup. Remove this boundary and a re-calling model never lets go.
        var claimed = new HashSet<string>();

        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "order", "call_1").ShouldBeTrue();
        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "order", "call_2").ShouldBeFalse();
        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "order", "call_3").ShouldBeFalse();
    }

    [Fact]
    public void EachToolShouldGetItsOwnRecovery()
    {
        // The bound is per tool, not per call: one tool burning its attempt must not silence another.
        var claimed = new HashSet<string>();

        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "order", "call_1").ShouldBeTrue();
        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "confirm_pickup_time", "call_2").ShouldBeTrue();
    }

    [Fact]
    public void AToolThatParsesSuccessfullyAgain_ShouldRegainItsRecovery()
    {
        // A transient malformed payload should not spend the attempt for the rest of the call.
        var claimed = new HashSet<string>();

        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "order", "call_1").ShouldBeTrue();
        AiSpeechAssistantConnectService.ReleaseArgumentRecovery(claimed, "order");

        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "order", "call_2").ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AFunctionCallWithNoCallId_ShouldNotClaimARecovery(string callId)
    {
        // A reply needs a call id to address. Without one the provider rejects the message, and on a
        // socket that is no longer open that rejection is classified critical and drops the call — so
        // the safe answer is the one that sends nothing, which is what happens today.
        var claimed = new HashSet<string>();

        AiSpeechAssistantConnectService.TryClaimArgumentRecovery(claimed, "order", callId).ShouldBeFalse();
        claimed.ShouldBeEmpty("a call that cannot be replied to must not spend the tool's one attempt");
    }
}
