using Serilog;
using Serilog.Context;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Logging;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// CallSid and StreamSid only arrive on Twilio's start frame, several logged steps into a call, so
/// the ambient log scope is opened without them and back-filled when they do.
///
/// <para>Two code paths receive that frame — the AI path and the forward-to-a-human path — and each
/// used to set the context fields itself. Only one of them also back-filled the scope, so a forwarded
/// call never carried CallSid on any line, and "filter by CallSid" silently returned nothing for that
/// whole category of call while the comment beside the other site promised every line had it. One
/// helper now does both, so the context and the scope cannot drift apart again.</para>
/// </summary>
public class TelephonyIdentifierCorrelationTests
{
    [Fact]
    public void ApplyTelephonyIdentifiers_ShouldSetTheContextAndTheAmbientScopeTogether()
    {
        using var correlator = TestCorrelator.CreateContext();

        var context = new AiSpeechAssistantConnectContext { LogScope = new DeferredLogScope() };

        using (LogContext.Push(context.LogScope))
        {
            Log.Information("before the start frame");

            AiSpeechAssistantConnectService.ApplyTelephonyIdentifiers(context, "CA123", "MZ456");

            Log.Information("after the start frame");
        }

        context.CallSid.ShouldBe("CA123");
        context.StreamSid.ShouldBe("MZ456");

        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

        events[0].Properties.ContainsKey(LogProperties.CallSid).ShouldBeFalse("the identifiers are not known yet");
        events[1].Properties[LogProperties.CallSid].ToString().ShouldBe("\"CA123\"");
        events[1].Properties[LogProperties.StreamSid].ToString().ShouldBe("\"MZ456\"");
    }

    [Fact]
    public void ApplyTelephonyIdentifiers_WithNoScopeOpen_ShouldStillSetTheContext()
    {
        // The scope is opened by the entry point. A caller that reaches this without one — a test, or a
        // future transport — must not lose the identifiers the record job and the transfer job read.
        var context = new AiSpeechAssistantConnectContext();

        Should.NotThrow(() => AiSpeechAssistantConnectService.ApplyTelephonyIdentifiers(context, "CA123", "MZ456"));

        context.CallSid.ShouldBe("CA123");
        context.StreamSid.ShouldBe("MZ456");
    }
}
