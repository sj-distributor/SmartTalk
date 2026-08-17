using NSubstitute;
using SmartTalk.Core.Services.AiKids;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Commands.AiKids;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class AiKidRealtimeWebRtcSessionTests
{
    [Fact]
    public async Task Initialize_AlwaysUsesBuiltInAudioForInterviewWebRtc()
    {
        var fixture = CreateFixture();
        fixture.Options.TtsConfig = new RealtimeAiTtsConfig
        {
            ProviderType = RealtimeAiTtsProviderType.MiniMax
        };

        await fixture.Session.InitializeAsync(
            625, RealtimeAiServerRegion.HK, "offer", CancellationToken.None, CancellationToken.None);

        Assert.Null(fixture.Options.TtsConfig);
        fixture.CallClient.Received(1).CreateCallAsync(
            "offer",
            Arg.Any<string>(),
            "wss://api.openai.com/v1/realtime?model=gpt-realtime-test",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FunctionCall_IsExecutedOnServerAndReplyTriggersNextResponse()
    {
        var fixture = CreateFixture();
        var functionCall = new RealtimeAiWssFunctionCallData
        {
            CallId = "call_1",
            FunctionName = "lookup_candidate",
            ArgumentsJson = "{}"
        };

        fixture.Options.OnFunctionCallAsync = (_, _) =>
            Task.FromResult(new RealtimeAiFunctionCallResult { Output = "candidate found" });
        fixture.Adapter.BuildFunctionCallReplyMessage(functionCall, "candidate found").Returns("function-output");
        fixture.Adapter.BuildTriggerResponseMessage().Returns("response-create");
        fixture.Adapter.ParseMessage("response-started").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseStarted
        });
        fixture.Adapter.ParseMessage("function-call").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.FunctionCallSuggested,
            Data = new List<RealtimeAiWssFunctionCallData> { functionCall }
        });

        await fixture.Session.InitializeAsync(
            625, RealtimeAiServerRegion.HK, "offer", CancellationToken.None, CancellationToken.None);
        await fixture.Session.ProcessProviderMessageAsync("response-started");
        await fixture.Session.ProcessProviderMessageAsync("function-call");

        Received.InOrder(() =>
        {
            fixture.Sideband.SendAsync("function-output", Arg.Any<CancellationToken>());
            fixture.Sideband.SendAsync("response-create", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SpeechStarted_UsesProviderNativeWebRtcInterruption_NotLegacyTruncate()
    {
        var fixture = CreateFixture();
        fixture.Adapter.ParseMessage("speech-started").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.SpeechDetected
        });

        await fixture.Session.InitializeAsync(
            625, RealtimeAiServerRegion.HK, "offer", CancellationToken.None, CancellationToken.None);
        await fixture.Session.ProcessProviderMessageAsync("speech-started");

        fixture.Timer.Received(1).StopTimer("rtc_test_123");
        fixture.Adapter.DidNotReceive().BuildTruncateMessage(Arg.Any<string>(), Arg.Any<long>());
        await fixture.Sideband.DidNotReceive().SendAsync(
            Arg.Is<string>(x => x != null && x.Contains("conversation.item.truncate")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FunctionCallbackFailure_DoesNotTearDownLaterEventProcessing()
    {
        var fixture = CreateFixture();
        fixture.Options.OnFunctionCallAsync = (_, _) =>
            throw new InvalidOperationException("function failed");
        fixture.Adapter.ParseMessage("function-call").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.FunctionCallSuggested,
            Data = new List<RealtimeAiWssFunctionCallData>
            {
                new() { CallId = "call_1", FunctionName = "bad_function", ArgumentsJson = "{}" }
            }
        });
        fixture.Adapter.ParseMessage("speech-started").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.SpeechDetected
        });

        await fixture.Session.InitializeAsync(
            625, RealtimeAiServerRegion.HK, "offer", CancellationToken.None, CancellationToken.None);
        await fixture.Session.ProcessProviderMessageAsync("function-call");
        await fixture.Session.ProcessProviderMessageAsync("speech-started");

        fixture.Timer.Received(1).StopTimer("rtc_test_123");
    }

    [Fact]
    public async Task ActiveResponseError_QueuesRejectedTriggerUntilCurrentResponseCompletes()
    {
        var fixture = CreateFixture();
        fixture.Adapter.BuildTriggerResponseMessage().Returns("response-create");
        fixture.Adapter.ParseMessage("active-response-error").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.Error,
            Data = new RealtimeAiErrorData
            {
                Code = "conversation_already_has_active_response",
                Message = "An active response is already in progress.",
                IsCritical = false
            }
        });
        fixture.Adapter.ParseMessage("response-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted
        });

        await fixture.Session.InitializeAsync(
            625, RealtimeAiServerRegion.HK, "offer", CancellationToken.None, CancellationToken.None);
        await fixture.Session.ProcessProviderMessageAsync("active-response-error");
        await fixture.Session.ProcessProviderMessageAsync("response-done");

        await fixture.Sideband.Received(1).SendAsync("response-create", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CriticalProviderError_StopsReceiveLoopAndCleansUpSession()
    {
        var fixture = CreateFixture();
        fixture.Adapter.ParseMessage("critical-error").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.Error,
            Data = new RealtimeAiErrorData
            {
                Code = "provider_failure",
                Message = "Provider session cannot continue.",
                IsCritical = true
            }
        });
        fixture.Sideband.RunReceiveLoopAsync(
                Arg.Any<Func<string, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.InfiniteTimeSpan, call.ArgAt<CancellationToken>(1)));

        await fixture.Session.InitializeAsync(
            625, RealtimeAiServerRegion.HK, "offer", CancellationToken.None, CancellationToken.None);
        var runTask = fixture.Session.RunAsync(CancellationToken.None);

        await fixture.Session.ProcessProviderMessageAsync("critical-error");
        await runTask.WaitAsync(TimeSpan.FromSeconds(1));

        await fixture.Sideband.Received(1).CloseAsync(
            "SmartTalk WebRTC session ended",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResponseTriggerSendFailure_RollsBackStateAndAllowsRetry()
    {
        var fixture = CreateFixture();
        RealtimeAiSessionActions? capturedActions = null;
        var triggerAttempts = 0;

        fixture.Options.OnSessionReadyAsync = actions =>
        {
            capturedActions = actions;
            return Task.CompletedTask;
        };
        fixture.Adapter.BuildTextUserMessage(Arg.Any<string>(), "rtc_test_123").Returns("text-input");
        fixture.Adapter.BuildTriggerResponseMessage().Returns("response-create");
        fixture.Adapter.ParseMessage("response-done").Returns(new ParsedRealtimeAiProviderEvent
        {
            Type = RealtimeAiWssEventType.ResponseTurnCompleted
        });
        fixture.Sideband.SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.ArgAt<string>(0) != "response-create") return Task.CompletedTask;

            return Interlocked.Increment(ref triggerAttempts) == 1
                ? Task.FromException(new InvalidOperationException("send failed"))
                : Task.CompletedTask;
        });

        await fixture.Session.InitializeAsync(
            625, RealtimeAiServerRegion.HK, "offer", CancellationToken.None, CancellationToken.None);
        await fixture.Session.MarkClientReadyAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capturedActions!.SendTextToProviderAsync("first"));
        await capturedActions!.SendTextToProviderAsync("second");

        Assert.Equal(2, triggerAttempts);

        await fixture.Session.ProcessProviderMessageAsync("response-done");

        Assert.Equal(2, triggerAttempts);
    }

    private static Fixture CreateFixture()
    {
        var aiKid = Substitute.For<IAiKidRealtimeServiceV2>();
        var switcher = Substitute.For<IRealtimeAiSwitcher>();
        var adapter = Substitute.For<IRealtimeAiProviderAdapter>();
        var callClient = Substitute.For<IOpenAiRealtimeWebRtcCallClient>();
        var sideband = Substitute.For<IOpenAiRealtimeWebRtcSidebandClient>();
        var timer = Substitute.For<IInactivityTimerManager>();

        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                ServiceUrl = "wss://api.openai.com/v1/realtime?model=gpt-realtime-test",
                ModelName = "gpt-realtime-test",
                Prompt = "prompt",
                Voice = "marin",
                TurnDetection = new { type = "server_vad" }
            },
            TtsConfig = new RealtimeAiTtsConfig
            {
                ProviderType = RealtimeAiTtsProviderType.BuiltIn
            }
        };

        aiKid.BuildSessionOptionsAsync(
                Arg.Any<AiKidRealtimeCommand>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<CancellationToken>())
            .Returns(options);
        switcher.ProviderAdapter(RealtimeAiProvider.OpenAi).Returns(adapter);
        adapter.BuildSessionConfig(
                Arg.Any<RealtimeSessionOptions>(),
                RealtimeAiOutputMode.Audio,
                RealtimeAiAudioCodec.PCM16)
            .Returns(new
            {
                type = "session.update",
                session = new
                {
                    type = "realtime",
                    instructions = "prompt",
                    output_modalities = new[] { "audio" },
                    audio = new
                    {
                        input = new { turn_detection = new { type = "server_vad" } },
                        output = new { voice = "marin" }
                    }
                }
            });
        callClient.CreateCallAsync(
                "offer",
                Arg.Any<string>(),
                "wss://api.openai.com/v1/realtime?model=gpt-realtime-test",
                Arg.Any<CancellationToken>())
            .Returns(new RealtimeAiWebRtcCallResult
            {
                CallId = "rtc_test_123",
                AnswerSdp = "answer",
                SidebandUri = new Uri("wss://api.openai.com/v1/realtime?call_id=rtc_test_123"),
                SidebandHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer test" }
            });

        var session = new AiKidRealtimeWebRtcSession(aiKid, switcher, callClient, sideband, timer);
        return new Fixture(session, options, adapter, callClient, sideband, timer);
    }

    private sealed record Fixture(
        AiKidRealtimeWebRtcSession Session,
        RealtimeSessionOptions Options,
        IRealtimeAiProviderAdapter Adapter,
        IOpenAiRealtimeWebRtcCallClient CallClient,
        IOpenAiRealtimeWebRtcSidebandClient Sideband,
        IInactivityTimerManager Timer);
}
