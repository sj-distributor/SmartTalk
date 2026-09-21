using NSubstitute;
using Serilog.Sinks.TestCorrelator;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The engine's session-start log used to destructure the whole session context with
/// <c>{@Context}</c>, which reached into <c>Options.ModelConfig.Prompt</c> — for a phone call that is
/// the fully resolved restaurant prompt, carrying the caller's number, whatever the CRM lookup
/// returned about them, and the menu. Every inbound call wrote that to Seq and to stdout, at
/// Information, before the caller heard the greeting.
///
/// <para>These tests are the standing guard: the session-start line must describe the session
/// without reproducing its content.</para>
/// </summary>
public class RealtimeAiServiceLogPrivacyTests : RealtimeAiServiceTestBase
{
    private const string SentinelPrompt = "SENTINEL-PROMPT-A1B2 caller +14155550123 ordered two spring rolls";

    private async Task RunSessionAsync(Action<RealtimeSessionOptions> customize = null)
    {
        var options = CreateDefaultOptions(o =>
        {
            o.ModelConfig.Prompt = SentinelPrompt;
            customize?.Invoke(o);
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NoLogLine_ShouldReproduceThePrompt()
    {
        using var context = TestCorrelator.CreateContext();

        await RunSessionAsync();

        var rendered = TestCorrelator.GetLogEventsFromCurrentContext()
            .Select(e => e.RenderMessage() + string.Join("|", e.Properties.Select(p => p.Value.ToString())))
            .ToList();

        rendered.ShouldNotBeEmpty();
        rendered.ShouldAllBe(text => !text.Contains("SENTINEL-PROMPT-A1B2"));
    }

    [Fact]
    public async Task SessionStartLog_ShouldDescribeThePromptWithoutQuotingIt()
    {
        using var context = TestCorrelator.CreateContext();

        await RunSessionAsync();

        var start = TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.MessageTemplate.Text.Contains("Session initialized"));

        start.Properties["PromptChars"].ToString().ShouldBe(SentinelPrompt.Length.ToString());
        start.Properties["ToolCount"].ToString().ShouldBe("0");
        start.Properties["Provider"].ToString().ShouldContain("OpenAi");

        // Lets an operator confirm which prompt revision was live without the sink retaining it.
        start.Properties["PromptSha256"].ToString().Trim('"').Length.ShouldBe(8);
    }

    [Fact]
    public async Task ProviderConnectedLog_ShouldReportTheNegotiatedOutputMode()
    {
        // The old {@Context} dump ran before OutputModeNegotiator had resolved anything, so it
        // reported Audio for every call ever made — actively misleading rather than merely absent.
        using var context = TestCorrelator.CreateContext();

        await RunSessionAsync();

        TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.MessageTemplate.Text.Contains("Connected to provider"))
            .Properties["OutputMode"].ToString().ShouldContain(nameof(RealtimeAiOutputMode.Audio));
    }

    [Fact]
    public async Task TextSentToProvider_ShouldBeLoggedAsALengthCappedPreview()
    {
        // Idle follow-ups and greeting instructions go through here, and a consumer is free to send
        // anything: the line must stay diagnostic without becoming a transcript of it.
        using var context = TestCorrelator.CreateContext();
        var longText = new string('x', 400) + "TAIL-SENTINEL";

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized });

        RealtimeAiSessionActions captured = null;
        var options = CreateDefaultOptions(o =>
        {
            o.ModelConfig.Prompt = SentinelPrompt;
            o.OnSessionReadyAsync = actions => { captured = actions; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("session.updated");

        captured.ShouldNotBeNull("the session-ready callback must have run for this test to mean anything");
        await captured.SendTextToProviderAsync(longText);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        var sendLine = TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.MessageTemplate.Text.Contains("Sending text to provider"));

        sendLine.RenderMessage().ShouldNotContain("TAIL-SENTINEL");
    }
}
