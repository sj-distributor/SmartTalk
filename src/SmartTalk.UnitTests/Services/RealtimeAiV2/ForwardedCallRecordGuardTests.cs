using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Every forwarded call's record job threw. <c>ForwardIfRequiredAsync</c> runs before
/// <c>BuildSessionConfigAsync</c> ever loads an assistant, so the context handed to the job has none,
/// and the job dereferenced it immediately. Hangfire retried until the job dead-lettered — the
/// failure was both invisible and repeated — while the Twilio recording callback, already triggered
/// at forward start, later found no record and threw in turn.
///
/// <para>Skipping explicitly stops the bleeding. It does not give forwarded calls a history row:
/// a phone-order record is keyed by an assistant, and inventing one would corrupt every downstream
/// report that joins on it. That gap needs a record shape that does not require an assistant.</para>
/// </summary>
public class ForwardedCallRecordGuardTests
{
    [Fact]
    public void ForwardedCallContext_ShouldNotBeRecordable()
    {
        // Exactly what HandleForwardStop builds.
        var forwarded = new AiSpeechAssistantStreamContextDto
        {
            CallSid = "CA-forwarded",
            StreamSid = "MZ-forwarded",
            IsTransfer = true,
            LastUserInfo = new AiSpeechAssistantUserInfoDto { PhoneNumber = "+14155550123" }
        };

        AiSpeechAssistantProcessJobService.CanRecordCall(forwarded).ShouldBeFalse();
    }

    [Fact]
    public void NormalCallContext_ShouldStillBeRecordable()
    {
        // The path that must keep working: a call that actually ran an assistant.
        var normal = new AiSpeechAssistantStreamContextDto
        {
            CallSid = "CA-normal",
            Assistant = new AiSpeechAssistantDto { Id = 42 }
        };

        AiSpeechAssistantProcessJobService.CanRecordCall(normal).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ContextWithoutACallSid_ShouldNotBeRecordable(string callSid)
    {
        // The record is keyed by CallSid; without one there is nothing to key or later reconcile.
        var context = new AiSpeechAssistantStreamContextDto
        {
            CallSid = callSid,
            Assistant = new AiSpeechAssistantDto { Id = 42 }
        };

        AiSpeechAssistantProcessJobService.CanRecordCall(context).ShouldBeFalse();
    }

    [Fact]
    public void NullContext_ShouldNotBeRecordable()
    {
        AiSpeechAssistantProcessJobService.CanRecordCall(null).ShouldBeFalse();
    }
}
