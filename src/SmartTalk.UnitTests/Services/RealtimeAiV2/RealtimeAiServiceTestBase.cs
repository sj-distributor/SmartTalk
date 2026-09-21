using System.Net.WebSockets;
using NSubstitute;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.BuiltIn;
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
    protected readonly IRealtimeAiTtsProvider TtsProvider;
    protected readonly IInactivityTimerManager TimerManager;
    protected readonly RealtimeAiService Sut;

    protected RealtimeAiServiceTestBase()
    {
        FakeWs = new FakeWebSocket();
        FakeWssClient = new FakeRealtimeAiWssClient();

        ProviderAdapter = Substitute.For<IRealtimeAiProviderAdapter>();
        ClientAdapter = Substitute.For<IRealtimeAiClientAdapter>();
        TtsProvider = new BuiltInRealtimeAiTtsProvider();
        Switcher = Substitute.For<IRealtimeAiSwitcher>();
        TimerManager = Substitute.For<IInactivityTimerManager>();

        // Wire switcher to return our fakes/mocks
        Switcher.WssClient(Arg.Any<RealtimeAiProvider>()).Returns(FakeWssClient);
        Switcher.ClientAdapter(Arg.Any<RealtimeAiClient>()).Returns(ClientAdapter);
        Switcher.ProviderAdapter(Arg.Any<RealtimeAiProvider>()).Returns(ProviderAdapter);
        Switcher.TtsProvider(Arg.Any<RealtimeAiTtsProviderType>()).Returns(TtsProvider);

        // Default stubs for ProviderAdapter
        ProviderAdapter.GetHeaders(Arg.Any<RealtimeAiServerRegion>())
            .Returns(new Dictionary<string, string> { { "Authorization", "Bearer test" } });
        ProviderAdapter.Capabilities.Returns(new RealtimeAiInferenceCapabilities
        {
            TextOutput = new RealtimeAiTextOutputSupport { CanEmitTextOnly = true, CanEmitTextAlongsideAudio = false },
            SupportsAudioOutput = true
        });
        ProviderAdapter.BuildSessionConfig(Arg.Any<RealtimeSessionOptions>(), Arg.Any<RealtimeAiOutputMode>(), Arg.Any<RealtimeAiAudioCodec>())
            .Returns(new { type = "session.update" });
        ProviderAdapter.BuildAudioAppendMessage(Arg.Any<RealtimeAiWssAudioData>())
            .Returns("audio_append_msg");
        ProviderAdapter.BuildTextUserMessage(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => $"text_user:{ci.ArgAt<string>(0)}");
        ProviderAdapter.BuildFunctionCallReplyMessage(Arg.Any<RealtimeAiWssFunctionCallData>(), Arg.Any<string>())
            .Returns(ci => $"fc_reply:{ci.ArgAt<string>(1)}");
        ProviderAdapter.BuildTriggerResponseMessage()
            .Returns("response_create_msg");
        ProviderAdapter.GetPreferredCodec(Arg.Any<RealtimeAiAudioCodec>())
            .Returns(ci => ci.ArgAt<RealtimeAiAudioCodec>(0));

        // Default stubs for ClientAdapter
        ClientAdapter.NativeAudioCodec.Returns(RealtimeAiAudioCodec.PCM16);
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
            Region = RealtimeAiServerRegion.US
        };

        customize?.Invoke(options);
        return options;
    }

    /// <summary>
    /// Starts ConnectAsync in the background and returns once the session is actually up — the read
    /// loop has reached its first ReceiveAsync — or once ConnectAsync has finished, whichever comes
    /// first. Returns the task so a test can later signal the FakeWs to close and await completion.
    ///
    /// <para>Waits on a signal rather than a fixed delay. A fixed delay is a race the test loses
    /// whenever the machine is busy: xunit runs test classes in parallel, so the background task may
    /// not have reached ConnectToProviderAsync yet, and any assertion the test makes immediately
    /// afterwards fails for reasons that have nothing to do with the behaviour under test.</para>
    /// </summary>
    protected async Task<Task> StartSessionInBackgroundAsync(RealtimeSessionOptions? options = null)
    {
        options ??= CreateDefaultOptions();

        var task = Task.Run(async () =>
        {
            try
            {
                await Sut.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        });

        // ConnectAsync completing first is legitimate: connect-failure tests never reach the loop.
        await Task.WhenAny(FakeWs.ReceiveStarted, task).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        return task;
    }

    public virtual void Dispose()
    {
        FakeWs.Dispose();
    }
}
