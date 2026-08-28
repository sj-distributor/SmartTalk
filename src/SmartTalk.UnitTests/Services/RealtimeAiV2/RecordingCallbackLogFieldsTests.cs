using Shouldly;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The recording-status callback used to log the whole PhoneOrderRecord with Serilog's destructuring
/// operator, which put the caller's phone number, the name they gave and their first spoken sentence
/// (the entity's Tips column) into Seq and stdout for every recorded call.
///
/// <para>Replacing that with named scalars introduced the one hazard worth pinning: the record is
/// legitimately null. A forwarded call triggers Twilio recording but deliberately never gets a row —
/// the same case <c>ForwardedCallRecordGuardTests</c> covers — and the lookup's retry returns that
/// null rather than throwing. If the accessors are not null-conditional the throw moves one line
/// EARLIER than it is today, the log event is never constructed, and the only in-log evidence of a
/// known live failure mode disappears.</para>
///
/// <para>Filed alongside the RealtimeAiV2 suite for the same reason ForwardedCallRecordGuardTests is:
/// it puts the guard inside the gate's fast subset filter.</para>
/// </summary>
public class RecordingCallbackLogFieldsTests
{
    [Fact]
    public void NoRecordForTheCallSid_ShouldResolveWithoutThrowing()
    {
        var (recordId, recordStatus, hasRecordingUrl) =
            Should.NotThrow(() => AiSpeechAssistantService.ResolveRecordingCallbackLogFields(null, "https://api.twilio.com/rec.wav"));

        recordId.ShouldBeNull();
        recordStatus.ShouldBeNull();
        hasRecordingUrl.ShouldBeTrue("the callback still carries a recording even when nothing was ever recorded against it");
    }

    [Fact]
    public void AnExistingRecord_ShouldReportItsIdAndStatus()
    {
        var record = new PhoneOrderRecord { Id = 42, Status = PhoneOrderRecordStatus.Transcription };

        var (recordId, recordStatus, _) = AiSpeechAssistantService.ResolveRecordingCallbackLogFields(record, "https://api.twilio.com/rec.wav");

        recordId.ShouldBe(42);
        recordStatus.ShouldBe(PhoneOrderRecordStatus.Transcription);
    }

    [Theory]
    [InlineData("https://api.twilio.com/rec.wav", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasRecordingUrl_ShouldComeFromTheCallbackNotTheRecord(string recordingUrl, bool expected)
    {
        // Read off the record instead, this would be false on every healthy call: the record's own Url
        // is assigned on the line AFTER the log. The callback's url is the signal an operator wants —
        // "did Twilio hand us audio" — and it is available before the assignment.
        var recordWithNoUrlYet = new PhoneOrderRecord { Id = 42, Url = null };

        var (_, _, hasRecordingUrl) = AiSpeechAssistantService.ResolveRecordingCallbackLogFields(recordWithNoUrlYet, recordingUrl);

        hasRecordingUrl.ShouldBe(expected);
    }
}
