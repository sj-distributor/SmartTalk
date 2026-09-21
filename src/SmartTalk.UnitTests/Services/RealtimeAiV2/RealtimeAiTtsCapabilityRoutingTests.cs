using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Core.Services.RealtimeAiV2.Negotiation;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The engine decided the session's output mode from the TTS provider's VENDOR LABEL — anything that
/// was not <c>BuiltIn</c> was assumed to need text input, and only <c>BuiltIn</c> got its output codec
/// negotiated with the client. Both facts are already declared structurally, by which of the two
/// capability siblings the provider implements, so the label was a second and weaker source of truth.
///
/// <para>The cost is extensibility, which is the whole point of splitting the inference and voice axes:
/// a new vendor cannot be added without editing the engine, and one that passes audio through would be
/// driven in text mode and produce a silently mute call. That is the exact failure
/// <c>OutputModeNegotiator</c> exists to prevent, arriving one layer above it — the negotiator can only
/// be as right as the capability flag it is handed.</para>
///
/// <para>These pin the decision against the capability rather than the label. The two vendors that
/// exist today map identically either way, so nothing about their behaviour changes.</para>
/// </summary>
public class RealtimeAiTtsCapabilityRoutingTests : RealtimeAiServiceTestBase
{
    /// <summary>Passes provider audio through, exactly like the built-in provider — but not labelled as it.</summary>
    private sealed class LabelMismatchedPassthroughTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiAudioPassthrough
    {
        public RealtimeAiTtsConfig InitializedWith { get; private set; }

        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;
        public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate => 24000;

        public event Func<string, Task> AudioChunkReadyAsync { add { } remove { } }
        public event Func<Task> SynthesisCompletedAsync { add { } remove { } }
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
        {
            InitializedWith = config;
            return Task.CompletedTask;
        }

        public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Implements neither sibling — it can be handed neither audio nor text and do anything useful.</summary>
    private sealed class CapabilitylessTtsProvider : IRealtimeAiTtsProvider
    {
        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;
        public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate => 24000;

        public event Func<string, Task> AudioChunkReadyAsync { add { } remove { } }
        public event Func<Task> SynthesisCompletedAsync { add { } remove { } }
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Can be driven either way — the shape a vendor adapter that supports both takes.</summary>
    private sealed class DualCapabilityTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiAudioPassthrough, IRealtimeAiTextSynthesizer
    {
        public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;
        public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;
        public int OutputSampleRate => 24000;

        public event Func<string, Task> AudioChunkReadyAsync { add { } remove { } }
        public event Func<Task> SynthesisCompletedAsync { add { } remove { } }
        public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync { add { } remove { } }

        public Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleProviderTextDoneAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleInterruptAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private RealtimeSessionOptions OptionsUsing(IRealtimeAiTtsProvider tts)
    {
        Switcher.TtsProvider(RealtimeAiTtsProviderType.MiniMax).Returns(tts);

        return CreateDefaultOptions(o =>
            o.TtsConfig = new RealtimeAiTtsConfig { ProviderType = RealtimeAiTtsProviderType.MiniMax, SampleRate = 24000 });
    }

    [Fact]
    public async Task APassthroughProviderNotLabelledBuiltIn_ShouldStillHaveItsCodecNegotiated()
    {
        // Only BuiltIn used to reach the codec negotiation, so a passthrough under any other label was
        // initialized with whatever codec the caller happened to configure — the client's own format
        // never entered into it.
        var tts = new LabelMismatchedPassthroughTtsProvider();

        ProviderAdapter.GetPreferredCodec(Arg.Any<RealtimeAiAudioCodec>()).Returns(RealtimeAiAudioCodec.MULAW);

        var sessionTask = await StartSessionInBackgroundAsync(OptionsUsing(tts));

        tts.InitializedWith.ShouldNotBeNull();
        tts.InitializedWith.TargetCodec.ShouldBe(RealtimeAiAudioCodec.MULAW,
            "a provider that passes audio through needs the codec the client actually speaks, whatever it is labelled");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task APassthroughProviderNotLabelledBuiltIn_ShouldRunInAudioMode()
    {
        // Driven in text mode, a passthrough provider is handed text it cannot synthesize and the call
        // goes silent — which is the failure OutputModeNegotiator exists to make impossible.
        var tts = new LabelMismatchedPassthroughTtsProvider();

        var sessionTask = await StartSessionInBackgroundAsync(OptionsUsing(tts));

        ProviderAdapter.Received().BuildSessionConfig(
            Arg.Any<RealtimeSessionOptions>(), RealtimeAiOutputMode.Audio, Arg.Any<RealtimeAiAudioCodec>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AProviderDeclaringBothCapabilities_ShouldNotBeTreatedAsRequiringText()
    {
        // "Requires text" is a statement about what the provider cannot do without. One that can also
        // pass audio through does not require it, and which of the two it is actually driven by is
        // OutputMode's decision. Pinned here because it is decided by the order of two type checks,
        // and the alternative reading — treating both as ambiguous and refusing — would reject a
        // provider that works, as RealtimeAiServiceAudioModeTextFlushGateGoldenTests shows.
        var tts = new DualCapabilityTtsProvider();

        ProviderAdapter.GetPreferredCodec(Arg.Any<RealtimeAiAudioCodec>()).Returns(RealtimeAiAudioCodec.MULAW);

        var sessionTask = await StartSessionInBackgroundAsync(OptionsUsing(tts));

        ProviderAdapter.Received().BuildSessionConfig(
            Arg.Any<RealtimeSessionOptions>(), RealtimeAiOutputMode.Audio, Arg.Any<RealtimeAiAudioCodec>());

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AProviderDeclaringNeitherCapability_ShouldFailLoudRatherThanMuteTheCall()
    {
        // Falling back to a mode it cannot serve is the silent-mute outcome under another name. The
        // engine's stance on an unusable pairing is already to throw; this is the same class of fault.
        var options = OptionsUsing(new CapabilitylessTtsProvider());

        // Bounded: without the guard this does not throw, it runs a live session and waits on a client
        // that never hangs up. A timeout is a failure here, not a hang.
        await Should.ThrowAsync<RealtimeAiOutputModeException>(
            () => Sut.ConnectAsync(options, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
