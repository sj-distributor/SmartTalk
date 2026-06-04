using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Pins the shape of the Hangfire-scheduled hangup expression to ensure
/// <see cref="System.Threading.CancellationToken.None"/> is captured —
/// not a closure-bound token from the calling scope.
///
/// <para>
/// Hangfire's expression serializer evaluates closure-captured values at
/// schedule time and stores them as constants in the job record. For
/// <see cref="System.Threading.CancellationToken"/> the serializer
/// substitutes <c>default</c> at deserialization regardless, so the runtime
/// behavior is the same. But:
/// </para>
/// <list type="bullet">
///   <item>If a future refactor changes the parameter type or upgrades
///         Hangfire, a captured token could leak the live request CTS into
///         a deferred job that fires after the request scope is disposed —
///         producing <c>ObjectDisposedException</c>.</item>
///   <item>The remaining <c>Schedule</c>/<c>Enqueue</c> calls in the same
///         partial class (<c>ProcessJobs.cs</c>, <c>Build.Session.cs</c>) all
///         use <c>CancellationToken.None</c>. This test pins the same intent
///         on the hangup path so the codebase stays uniform.</item>
/// </list>
/// </summary>
public class HangupJobExpressionTests
{
    [Fact]
    public void BuildHangupJobExpression_ProducesCallToHangupCallAsyncWithGivenCallSid()
    {
        var expr = AiSpeechAssistantConnectService.BuildHangupJobExpression("CA1234567890");

        var fakeService = Substitute.For<IAiSpeechAssistantService>();
        _ = expr.Compile()(fakeService);

        fakeService.Received(1).HangupCallAsync("CA1234567890", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildHangupJobExpression_PassesCancellationTokenNone_NotCapturedToken()
    {
        var expr = AiSpeechAssistantConnectService.BuildHangupJobExpression("CA-test");

        var fakeService = Substitute.For<IAiSpeechAssistantService>();
        _ = expr.Compile()(fakeService);

        // Strict assertion: must be CancellationToken.None — not any other token.
        fakeService.Received(1).HangupCallAsync(Arg.Any<string>(), CancellationToken.None);
    }

    [Theory]
    [InlineData("CA-abcdef")]
    [InlineData("")]
    [InlineData(null)]
    public void BuildHangupJobExpression_PreservesCallSidVerbatim(string callSid)
    {
        var expr = AiSpeechAssistantConnectService.BuildHangupJobExpression(callSid);

        var fakeService = Substitute.For<IAiSpeechAssistantService>();
        _ = expr.Compile()(fakeService);

        fakeService.Received(1).HangupCallAsync(callSid, CancellationToken.None);
    }
}
