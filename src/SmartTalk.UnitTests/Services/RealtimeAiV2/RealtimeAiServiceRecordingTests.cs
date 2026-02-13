using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceRecordingTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task Recording_Enabled_AudioBufferedDuringSession()
    {
        byte[]? recordedWav = null;

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }) });

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (sessionId, wavBytes) =>
            {
                recordedWav = wavBytes;
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"AQIDBA==\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        recordedWav.ShouldNotBeNull();
        recordedWav!.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Recording_SessionEnd_OnRecordingCompleteAsyncInvokedWithWavBytes()
    {
        byte[]? recordedWav = null;

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = Convert.ToBase64String(new byte[] { 10, 20, 30, 40 }) });

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (sessionId, wavBytes) =>
            {
                recordedWav = wavBytes;
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"test\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        recordedWav.ShouldNotBeNull();
        // WAV file starts with RIFF header
        System.Text.Encoding.ASCII.GetString(recordedWav!, 0, 4).ShouldBe("RIFF");
    }

    [Fact]
    public async Task Recording_Disabled_NoBufferCreated()
    {
        var callbackInvoked = false;

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = false;
            o.OnRecordingCompleteAsync = (sessionId, wavBytes) =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClose();
        await sessionTask;

        callbackInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task Recording_OnRecordingCompleteAsyncNull_NoException()
    {
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = Convert.ToBase64String(new byte[] { 1, 2 }) });

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = null; // No callback
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"AQI=\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();

        // Should not throw
        await Should.NotThrowAsync(() => sessionTask);
    }

    [Fact]
    public async Task Recording_AiAudioAlsoBuffered()
    {
        byte[]? recordedWav = null;

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseAudioDelta,
                Data = new RealtimeAiWssAudioData
                {
                    Base64Payload = Convert.ToBase64String(new byte[] { 50, 60, 70, 80 })
                }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (sessionId, wavBytes) =>
            {
                recordedWav = wavBytes;
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.audio.delta\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        recordedWav.ShouldNotBeNull();
        recordedWav!.Length.ShouldBeGreaterThan(44); // WAV header is 44 bytes, should have data too
    }

    [Fact]
    public async Task Recording_EnabledButNoAudio_CallbackNotInvoked()
    {
        var callbackInvoked = false;

        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (_, _) => { callbackInvoked = true; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // Close immediately without sending any audio
        FakeWs.EnqueueClose();
        await sessionTask;

        // HandleRecordingAsync should return early because snapshot.Length == 0
        callbackInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task Recording_UserAudioAfterSpeechDetected_BufferedForRecording()
    {
        // This test verifies the fix: after SpeechDetected (user interrupts AI),
        // IsAiSpeaking resets to false so subsequent user audio IS buffered for recording.
        //
        // Sequence: AI audio delta → SpeechDetected → User sends audio → Close
        // Expected: WAV contains both AI audio (4 bytes) AND user audio (200 bytes).

        var aiAudioBytes = new byte[] { 1, 2, 3, 4 };
        var aiAudioBase64 = Convert.ToBase64String(aiAudioBytes);

        var userAudioBytes = new byte[200];
        Array.Fill<byte>(userAudioBytes, 0xBB);
        var userAudioBase64 = Convert.ToBase64String(userAudioBytes);

        var providerCallIndex = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                providerCallIndex++;
                return providerCallIndex == 1
                    ? new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.ResponseAudioDelta,
                        Data = new RealtimeAiWssAudioData { Base64Payload = aiAudioBase64 }
                    }
                    : new ParsedRealtimeAiProviderEvent
                    {
                        Type = RealtimeAiWssEventType.SpeechDetected
                    };
            });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = userAudioBase64 });

        byte[]? recordedWav = null;
        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (_, wav) => { recordedWav = wav; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // 1. Provider sends audio delta → IsAiSpeaking=true, AI audio (4 bytes) buffered
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.audio.delta\"}");
        await Task.Delay(50);

        // 2. Provider sends SpeechDetected → IsAiSpeaking should reset to false
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"input_audio_buffer.speech_started\"}");
        await Task.Delay(50);

        // 3. Client sends audio AFTER speech detected → should be buffered (200 bytes)
        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"user_audio\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // WAV should contain BOTH AI audio (4 bytes) and user audio (200 bytes) plus header (~46 bytes).
        // Total ≈ 250 bytes. If IsAiSpeaking wasn't reset, user audio would be missing → ~50 bytes.
        recordedWav.ShouldNotBeNull();
        recordedWav!.Length.ShouldBeGreaterThan(200);
    }
}
