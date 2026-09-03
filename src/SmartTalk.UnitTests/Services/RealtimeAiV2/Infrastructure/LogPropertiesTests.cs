using Shouldly;
using SmartTalk.Core.Logging;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Infrastructure;

/// <summary>
/// Hard-pins the ambient property names.
///
/// <para>These strings are what Seq queries, saved signals and dashboards filter on. Renaming a
/// constant is a one-character edit that compiles cleanly and silently breaks every stored query an
/// on-call engineer relies on — exactly the class of change that has to be a visible decision rather
/// than an invisible refactor.</para>
/// </summary>
public class LogPropertiesTests
{
    [Theory]
    [InlineData(nameof(LogProperties.RealtimeSessionId), "RealtimeSessionId")]
    [InlineData(nameof(LogProperties.CallSid), "CallSid")]
    [InlineData(nameof(LogProperties.StreamSid), "StreamSid")]
    [InlineData(nameof(LogProperties.AgentId), "AgentId")]
    [InlineData(nameof(LogProperties.AssistantId), "AssistantId")]
    public void PropertyName_ShouldKeepItsWireValue(string constantName, string expectedValue)
    {
        typeof(LogProperties).GetField(constantName)!.GetValue(null).ShouldBe(expectedValue);
    }

    [Fact]
    public void NoAmbientPropertyShouldCarryCallerIdentity()
    {
        // The ambient scope rides every log event inside a call — all ~140 sites reachable from the
        // engine, the consumer and both transport receive loops — so anything named here is stamped on
        // the whole call rather than on the handful of lines that mean to report it. The caller's and
        // the restaurant's numbers were briefly here; a correlation key is the only thing that belongs.
        var callerIdentity = new[] { "From", "To", "PhoneNumber", "CustomerName" };

        typeof(LogProperties).GetFields()
            .Select(f => (string)f.GetValue(null))
            .Intersect(callerIdentity)
            .ShouldBeEmpty("caller identity must stay on the specific lines that name it, never on the ambient scope");
    }
}
