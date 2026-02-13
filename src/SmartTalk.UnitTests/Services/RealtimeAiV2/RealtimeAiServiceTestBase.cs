using System.Net.WebSockets;
using NSubstitute;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.UnitTests.Services.RealtimeAiV2.Fakes;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Shared setup for all RealtimeAiService tests.
/// Creates fresh SUT, fakes, and mocks per test.
/// </summary>
public abstract class RealtimeAiServiceTestBase : IDisposable
{
    protected readonly FakeWebSocket FakeWs;
    protected readonly FakeRealtimeAiWssClient FakeWssClient;
    protected readonly IRealtimeAiSwitcher Switcher;
    protected readonly IRealtimeAiProviderAdapter ProviderAdapter;
    protected readonly IRealtimeAiClientAdapter ClientAdapter;
    protected readonly IInactivityTimerManager TimerManager;
    protected readonly RealtimeAiService Sut;

    protected RealtimeAiServiceTestBase()
    {
        FakeWs = new FakeWebSocket();
        FakeWssClient = new FakeRealtimeAiWssClient();

        ProviderAdapter = Substitute.For<IRealtimeAiProviderAdapter>();
        ClientAdapter = Substitute.For<IRealtimeAiClientAdapter>();
        Switcher = Substitute.For<IRealtimeAiSwitcher>();
        TimerManager = Substitute.For<IInactivityTimerManager>();

        // Wire switcher to return our fakes/mocks
        Switcher.WssClient(Arg.Any<RealtimeAiProvider>()).Returns(FakeWssClient);
        Switcher.ClientAdapter(Arg.Any<RealtimeAiClient>()).Returns(ClientAdapter);
        Switcher.ProviderAdapter(Arg.Any<RealtimeAiProvider>()).Returns(ProviderAdapter);

        // Default stubs for ProviderAdapter
        ProviderAdapter.GetHeaders(Arg.Any<RealtimeAiServerRegion>())
            .Returns(new Dictionary<string, string> { { "Authorization", "Bearer test" } });
        ProviderAdapter.BuildSessionConfig(Arg.Any<RealtimeSessionOptions>())
            .Returns(new { type = "session.update" });
        ProviderAdapter.BuildAudioAppendMessage(Arg.Any<RealtimeAiWssAudioData>())
            .Returns("audio_append_msg");
        ProviderAdapter.BuildTextUserMessage(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => $"text_user:{ci.ArgAt<string>(0)}");
        ProviderAdapter.BuildFunctionCallReplyMessage(Arg.Any<RealtimeAiWssFunctionCallData>(), Arg.Any<string>())
            .Returns(ci => $"fc_reply:{ci.ArgAt<string>(1)}");
        ProviderAdapter.BuildTriggerResponseMessage()
            .Returns("response_create_msg");

        // Default stubs for ClientAdapter
        ClientAdapter.BuildAudioDeltaMessage(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => new { type = "ResponseAudioDelta", data = ci.ArgAt<string>(0) });
        ClientAdapter.BuildSpeechDetectedMessage(Arg.Any<string>())
            .Returns(new { type = "SpeechDetected" });
        ClientAdapter.BuildTurnCompletedMessage(Arg.Any<string>())
            .Returns(new { type = "AiTurnCompleted" });
        ClientAdapter.BuildTranscriptionMessage(Arg.Any<RealtimeAiWssEventType>(), Arg.Any<RealtimeAiWssTranscriptionData>(), Arg.Any<string>())
            .Returns(new { type = "Transcription" });
        ClientAdapter.BuildErrorMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => new { type = "ClientError", code = ci.ArgAt<string>(0), message = ci.ArgAt<string>(1) });

        Sut = new RealtimeAiService(Switcher, TimerManager);
    }

    /// <summary>Create options with sensible defaults, optionally customized.</summary>
    protected RealtimeSessionOptions CreateDefaultOptions(Action<RealtimeSessionOptions>? customize = null)
    {
        var options = new RealtimeSessionOptions
        {
            WebSocket = FakeWs,
            ClientConfig = new RealtimeAiClientConfig { Client = RealtimeAiClient.Default },
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                ServiceUrl = "wss://api.openai.com/v1/realtime",
                Voice = "alloy",
                ModelName = "gpt-4o-realtime",
                Prompt = "You are a helpful assistant."
            },
            ConnectionProfile = new RealtimeAiConnectionProfile { ProfileId = "test-profile" },
            InputFormat = RealtimeAiAudioCodec.PCM16,
            OutputFormat = RealtimeAiAudioCodec.PCM16,
            Region = RealtimeAiServerRegion.US
        };

        customize?.Invoke(options);
        return options;
    }

    /// <summary>
    /// Start ConnectAsync on a background thread and wait for the read loop to begin.
    /// Returns the task so the test can later signal the FakeWs to close and await completion.
    /// </summary>
    protected async Task<Task> StartSessionInBackgroundAsync(RealtimeSessionOptions? options = null)
    {
        options ??= CreateDefaultOptions();

        var sessionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The read loop will block on ReceiveAsync, so once ConnectAsync reaches OrchestrateSessionAsync
        // and calls ReceiveAsync for the first time, we know the session is running.
        // We give a small delay to ensure the loop has entered.
        var task = Task.Run(async () =>
        {
            try
            {
                // Signal that we're about to start
                _ = Task.Delay(50).ContinueWith(_ => sessionStarted.TrySetResult());
                await Sut.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                sessionStarted.TrySetException(ex);
            }
        });

        await sessionStarted.Task.ConfigureAwait(false);
        // Extra small delay to ensure the read loop has entered ReceiveAsync
        await Task.Delay(50).ConfigureAwait(false);
        return task;
    }

    public virtual void Dispose()
    {
        FakeWs.Dispose();
    }
}
