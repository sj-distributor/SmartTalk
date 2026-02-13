using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceSessionLifecycleTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task Session_ClientSendsAudio_ForwardedToProvider()
    {
        var validBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns((RealtimeAiClientMessageType.Audio, validBase64));

        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"AQIDBA==\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // BuildAudioAppendMessage should have been called and sent to provider
        ProviderAdapter.Received().BuildAudioAppendMessage(Arg.Any<RealtimeAiWssAudioData>());
        FakeWssClient.SentMessages.ShouldContain("audio_append_msg");
    }

    [Fact]
    public async Task Session_ClientSendsText_ForwardedToProviderWithResponseCreate()
    {
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns((RealtimeAiClientMessageType.Text, "hello world"));

        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"text\":\"hello world\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Both text message and response.create should have been sent
        ProviderAdapter.Received().BuildTextUserMessage("hello world", Arg.Any<string>());
        ProviderAdapter.Received().BuildTriggerResponseMessage();
        FakeWssClient.SentMessages.ShouldContain("text_user:hello world");
        FakeWssClient.SentMessages.ShouldContain("response_create_msg");
    }

    [Fact]
    public async Task Session_ProviderSendsSessionInitialized_InvokesOnSessionReadyAsync()
    {
        Func<string, Task>? capturedSendText = null;

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized });

        var options = CreateDefaultOptions(o =>
        {
            o.OnSessionReadyAsync = async sendText =>
            {
                capturedSendText = sendText;
                await Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"session.created\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        capturedSendText.ShouldNotBeNull();
    }

    [Fact]
    public async Task Session_ProviderSendsAudioDelta_ForwardedToClient()
    {
        var validBase64 = Convert.ToBase64String(new byte[] { 10, 20, 30 });
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseAudioDelta,
                Data = new RealtimeAiWssAudioData { Base64Payload = validBase64 }
            });

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.audio.delta\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        ClientAdapter.Received().BuildAudioDeltaMessage(validBase64, Arg.Any<string>());
        FakeWs.GetSentTextMessages().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Session_ProviderSendsTranscriptionCompleted_QueuedAndSentToClient()
    {
        var transcriptions = new List<(AiSpeechAssistantSpeaker, string)>();

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.InputAudioTranscriptionCompleted,
                Data = new RealtimeAiWssTranscriptionData
                {
                    Speaker = AiSpeechAssistantSpeaker.User,
                    Transcript = "Hello there"
                }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnTranscriptionsCompletedAsync = (sessionId, t) =>
            {
                transcriptions.AddRange(t);
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"transcription.completed\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Transcription should have been queued and delivered at session end
        transcriptions.ShouldNotBeEmpty();
        transcriptions[0].Item2.ShouldBe("Hello there");

        // Also sent to client
        ClientAdapter.Received().BuildTranscriptionMessage(
            RealtimeAiWssEventType.InputAudioTranscriptionCompleted,
            Arg.Any<RealtimeAiWssTranscriptionData>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Session_ProviderSendsPartialTranscription_SentToClientButNotQueued()
    {
        var transcriptions = new List<(AiSpeechAssistantSpeaker, string)>();

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.OutputAudioTranscriptionPartial,
                Data = new RealtimeAiWssTranscriptionData
                {
                    Speaker = AiSpeechAssistantSpeaker.Ai,
                    Transcript = "Partial text"
                }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnTranscriptionsCompletedAsync = (sessionId, t) =>
            {
                transcriptions.AddRange(t);
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"transcription.partial\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Partial transcription should NOT be in the final collection
        transcriptions.ShouldBeEmpty();

        // But still sent to client for real-time display
        ClientAdapter.Received().BuildTranscriptionMessage(
            RealtimeAiWssEventType.OutputAudioTranscriptionPartial,
            Arg.Any<RealtimeAiWssTranscriptionData>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Session_ProviderSendsTurnCompleted_RoundIncrementedAndSentToClient()
    {
        var turnCompletedCount = 0;

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseTurnCompleted,
                Data = new List<RealtimeAiWssFunctionCallData>() // empty = no function calls
            });

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(50);
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // BuildTurnCompletedMessage should have been called for each turn
        ClientAdapter.Received(2).BuildTurnCompletedMessage(Arg.Any<string>());
    }

    [Fact]
    public async Task Session_ClientCloses_CleanupRunsCallbacks()
    {
        string? endedSessionId = null;

        var options = CreateDefaultOptions(o =>
        {
            o.OnSessionEndedAsync = sessionId =>
            {
                endedSessionId = sessionId;
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClose();
        await sessionTask;

        endedSessionId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Session_OnSessionEndedAsync_ReceivesSessionId()
    {
        string? receivedSessionId = null;

        var options = CreateDefaultOptions(o =>
        {
            o.OnSessionEndedAsync = sessionId =>
            {
                receivedSessionId = sessionId;
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueueClose();
        await sessionTask;

        receivedSessionId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(receivedSessionId, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Session_OnTranscriptionsCompletedAsync_ReceivesCollectedTranscriptions()
    {
        IReadOnlyList<(AiSpeechAssistantSpeaker Speaker, string Text)>? receivedTranscriptions = null;

        var callIndex = 0;
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                callIndex++;
                return new ParsedRealtimeAiProviderEvent
                {
                    Type = RealtimeAiWssEventType.OutputAudioTranscriptionCompleted,
                    Data = new RealtimeAiWssTranscriptionData
                    {
                        Speaker = AiSpeechAssistantSpeaker.Ai,
                        Transcript = $"Sentence {callIndex}"
                    }
                };
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnTranscriptionsCompletedAsync = (sessionId, transcriptions) =>
            {
                receivedTranscriptions = transcriptions;
                return Task.CompletedTask;
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"transcription1\"}");
        await Task.Delay(50);
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"transcription2\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        receivedTranscriptions.ShouldNotBeNull();
        receivedTranscriptions!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Session_ClientSendsImage_ForwardedToProviderWithImageProperty()
    {
        var validBase64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF }); // JPEG magic bytes
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns((RealtimeAiClientMessageType.Image, validBase64));

        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"media\":{\"type\":\"video\",\"payload\":\"test\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Image is forwarded via BuildAudioAppendMessage (same channel, different CustomProperties)
        ProviderAdapter.Received().BuildAudioAppendMessage(
            Arg.Is<RealtimeAiWssAudioData>(d =>
                d.Base64Payload == validBase64 &&
                d.CustomProperties.ContainsKey("image")));
    }

    [Fact]
    public async Task Session_ClientSendsUnknownType_SessionContinues()
    {
        var callCount = 0;
        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? (RealtimeAiClientMessageType.Unknown, (string?)null)
                    : (RealtimeAiClientMessageType.Text, "hello");
            });

        var sessionTask = await StartSessionInBackgroundAsync();

        // First message: unknown type → logged and skipped
        FakeWs.EnqueueClientMessage("{\"unknown\":true}");
        await Task.Delay(50);

        // Second message: valid text → should be processed, proving session survived
        FakeWs.EnqueueClientMessage("{\"text\":\"hello\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        ProviderAdapter.Received().BuildTextUserMessage("hello", Arg.Any<string>());
    }

    [Fact]
    public async Task Session_AudioNotBufferedWhileAiSpeaking_ButStillForwarded()
    {
        var aiAudioBytes = new byte[] { 1, 2, 3, 4 };
        var aiAudioBase64 = Convert.ToBase64String(aiAudioBytes);
        // Use a much larger user audio payload so the size difference is unmistakable
        var userAudioBytes = new byte[200];
        Array.Fill<byte>(userAudioBytes, 0xAA);
        var userAudioBase64 = Convert.ToBase64String(userAudioBytes);

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseAudioDelta,
                Data = new RealtimeAiWssAudioData { Base64Payload = aiAudioBase64 }
            });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns((RealtimeAiClientMessageType.Audio, userAudioBase64));

        byte[]? recordedWav = null;
        var options = CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            o.OnRecordingCompleteAsync = (_, wav) => { recordedWav = wav; return Task.CompletedTask; };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // 1. Provider sends audio delta → IsAiSpeaking=true, AI audio buffered (4 bytes)
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.audio.delta\"}");
        await Task.Delay(50);

        // 2. Client sends audio while AI is speaking → NOT buffered, but still forwarded
        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"test\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // User audio should still have been forwarded to provider
        ProviderAdapter.Received().BuildAudioAppendMessage(Arg.Any<RealtimeAiWssAudioData>());

        // WAV contains only AI audio (4 bytes) + WAV header (~46 bytes) ≈ 50 bytes.
        // If user audio (200 bytes) was incorrectly buffered, it would be ≈ 250 bytes.
        recordedWav.ShouldNotBeNull();
        recordedWav!.Length.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task Session_ProviderSendsEmptyAudioDelta_Ignored()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.ResponseAudioDelta,
                Data = new RealtimeAiWssAudioData { Base64Payload = "" }
            });

        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.audio.delta\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Empty audio data should be ignored — no BuildAudioDeltaMessage call to client
        ClientAdapter.DidNotReceive().BuildAudioDeltaMessage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Session_TriggerResponseReturnsNull_OnlyTextSentToProvider()
    {
        // Some providers (e.g. Google) auto-trigger responses; BuildTriggerResponseMessage returns null
        ProviderAdapter.BuildTriggerResponseMessage().Returns((string?)null);

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns((RealtimeAiClientMessageType.Text, "hello"));

        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"text\":\"hello\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Text message should be sent
        FakeWssClient.SentMessages.ShouldContain(m => m.StartsWith("text_user:"));
        // But null trigger message should NOT appear (SendToProviderAsync skips nulls)
        FakeWssClient.SentMessages.ShouldNotContain(m => m == "response_create_msg");
    }
}
