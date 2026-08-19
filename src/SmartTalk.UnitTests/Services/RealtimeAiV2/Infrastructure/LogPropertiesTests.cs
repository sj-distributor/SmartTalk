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
    [InlineData(nameof(LogProperties.From), "From")]
    [InlineData(nameof(LogProperties.To), "To")]
    public void PropertyName_ShouldKeepItsWireValue(string constantName, string expectedValue)
    {
        typeof(LogProperties).GetField(constantName)!.GetValue(null).ShouldBe(expectedValue);
    }
}
