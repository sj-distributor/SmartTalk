using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeHttp;
using SmartTalk.Core.Settings.RealtimeHttp;
using SmartTalk.Messages.Dto.RealtimeHttp;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeHttp;

public class RealtimeHttpGatewayTests
{
    [Fact]
    public void GatewayExceptionFactories_ReturnStableCodesAndStatuses()
    {
        RealtimeHttpGatewayException.SessionNotFound("missing").StatusCode.ShouldBe(HttpStatusCode.NotFound);
        RealtimeHttpGatewayException.SessionClosed("closed", "provider", "idle").StatusCode.ShouldBe(HttpStatusCode.Gone);
        RealtimeHttpGatewayException.SessionBusy("busy").StatusCode.ShouldBe(HttpStatusCode.Conflict);
        RealtimeHttpGatewayException.TtsUnavailable("tts").StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        RealtimeHttpGatewayException.AiResponseTimeout("timeout", "provider", "InputAudioTranscriptionCompleted").StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);

        RealtimeHttpGatewayException.SessionBusy("busy").Code.ShouldBe("session_busy");
        RealtimeHttpGatewayException.TtsUnavailable("tts").Code.ShouldBe("tts_unavailable");
    }

    [Fact]
    public async Task SendTextAsync_WaitsForAiTurnCompletedAndReturnsAssistantTranscript()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(transport, new FakeTtsService(PcmBytesForMilliseconds(100)));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(2);

        transport.EnqueueInbound(AssistantTranscript("provider-1", "hi there"));
        transport.EnqueueInbound(TurnCompleted("provider-1"));

        var response = await sendTask;

        response.Completed.ShouldBeTrue();
        response.OutputText.ShouldBe("hi there");
        response.ProviderSessionId.ShouldBe("provider-1");
        response.CompletionReason.ShouldBe("ai_turn_completed");
        response.InputAudioDurationMs.ShouldBe(100);
        response.TailSilenceMs.ShouldBe(0);
        response.LastEventType.ShouldBe("AiTurnCompleted");
        GetRecordingPayloadByteCount(transport.SentMessages[0]).ShouldBe(200);
        GetTextPayload(transport.SentMessages[1]).ShouldBe("hello");
    }

    [Fact]
    public async Task SendTextAsync_WhenAssistantTurnIsInProgress_WaitsBeforeInjectingUserAudioAndText()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(transport, new FakeTtsService(PcmBytesForMilliseconds(100)));
        session.StartReceiving();
        transport.EnqueueInbound(ResponseAudioDelta("provider-1"));

        await Task.Delay(20);
        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await Task.Delay(80);

        transport.SentMessages.ShouldBeEmpty();

        transport.EnqueueInbound(AssistantTranscript("provider-1", "greeting"));
        transport.EnqueueInbound(TurnCompleted("provider-1"));
        await transport.WaitForSentCountAsync(2);

        GetRecordingPayloadByteCount(transport.SentMessages[0]).ShouldBe(200);
        GetTextPayload(transport.SentMessages[1]).ShouldBe("hello");

        transport.EnqueueInbound(AssistantTranscript("provider-1", "reply"));
        transport.EnqueueInbound(TurnCompleted("provider-1"));

        var response = await sendTask;

        response.Completed.ShouldBeTrue();
        response.OutputText.ShouldBe("reply");
        response.TurnNumber.ShouldBe(2);
    }

    [Fact]
    public async Task SendTextAsync_WhenTextInputIsDisabled_SendsSynthesizedAudioWithTailSilence()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(
            transport,
            new FakeTtsService(PcmBytesForMilliseconds(100)),
            settings: CreateSettings(sendTextAsTextInput: false));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(1);

        transport.EnqueueInbound(AssistantTranscript("provider-1", "hi there"));
        transport.EnqueueInbound(TurnCompleted("provider-1"));

        var response = await sendTask;

        response.Completed.ShouldBeTrue();
        response.InputAudioDurationMs.ShouldBe(100);
        response.TailSilenceMs.ShouldBe(40);
        GetPayloadByteCount(transport.SentMessages[0]).ShouldBe(280);
    }

    [Fact]
    public async Task SendTextAsync_WhenTtsReturnsNoAudio_ThrowsTtsUnavailable()
    {
        var session = CreateSession(
            new FakeRealtimeHttpGatewayTransport(),
            new FakeTtsService([]));
        session.StartReceiving();

        var ex = await Should.ThrowAsync<RealtimeHttpGatewayException>(() =>
            session.SendTextAsync("hello", 1000, CancellationToken.None));

        ex.Code.ShouldBe("tts_unavailable");
        ex.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task SendTextAsync_WhenTextInputIsDisabledAndTtsReturnsNoAudio_ThrowsTtsUnavailable()
    {
        var session = CreateSession(
            new FakeRealtimeHttpGatewayTransport(),
            new FakeTtsService([]),
            settings: CreateSettings(sendTextAsTextInput: false));
        session.StartReceiving();

        var ex = await Should.ThrowAsync<RealtimeHttpGatewayException>(() =>
            session.SendTextAsync("hello", 1000, CancellationToken.None));

        ex.Code.ShouldBe("tts_unavailable");
        ex.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task SendTextAsync_WhenAnotherTurnIsProcessing_ThrowsSessionBusy()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(transport, new FakeTtsService(PcmBytesForMilliseconds(100)));
        session.StartReceiving();

        var firstSend = session.SendTextAsync("first", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(2);

        var ex = await Should.ThrowAsync<RealtimeHttpGatewayException>(() =>
            session.SendTextAsync("second", 1000, CancellationToken.None));

        ex.Code.ShouldBe("session_busy");
        ex.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        transport.EnqueueInbound(AssistantTranscript("provider-1", "done"));
        transport.EnqueueInbound(TurnCompleted("provider-1"));
        await firstSend;
    }

    [Fact]
    public async Task SendTextAsync_WhenAiTurnDoesNotComplete_ThrowsTimeoutWithDiagnostics()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(transport, new FakeTtsService(PcmBytesForMilliseconds(20)));
        session.StartReceiving();

        var ex = await Should.ThrowAsync<RealtimeHttpGatewayException>(() =>
            session.SendTextAsync("hello", 20, CancellationToken.None));

        ex.Code.ShouldBe("ai_response_timeout");
        ex.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
    }

    [Fact]
    public async Task SendTextAsync_WhenProviderReturnsClientError_FailsImmediately()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(transport, new FakeTtsService(PcmBytesForMilliseconds(20)));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(2);
        transport.EnqueueInbound(ClientError("provider-1", "provider said no"));

        var ex = await Should.ThrowAsync<RealtimeHttpGatewayException>(async () => await sendTask);

        ex.Code.ShouldBe("provider_error");
        ex.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        ex.ProviderSessionId.ShouldBe("provider-1");
        ex.Message.ShouldBe("provider said no");
    }

    [Fact]
    public async Task SendTextAsync_WhenSessionClosesDuringTurn_ReturnsSessionClosed()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(transport, new FakeTtsService(PcmBytesForMilliseconds(20)));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(2);
        await session.CloseAsync("manual_close", CancellationToken.None);

        var ex = await Should.ThrowAsync<RealtimeHttpGatewayException>(async () => await sendTask);

        ex.Code.ShouldBe("session_closed");
        ex.StatusCode.ShouldBe(HttpStatusCode.Gone);
        ex.Reason.ShouldBe("manual_close");
    }

    [Fact]
    public async Task SendTextAsync_WhenSessionClosesDuringTts_ReturnsSessionClosedWithoutSendingAudio()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var ttsService = new BlockingTtsService(PcmBytesForMilliseconds(20));
        var session = CreateSession(transport, ttsService, settings: CreateSettings(sendTextAsTextInput: false));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await ttsService.WaitStartedAsync();
        await session.CloseAsync("manual_close", CancellationToken.None);

        var ex = await Should.ThrowAsync<RealtimeHttpGatewayException>(async () => await sendTask);

        ex.Code.ShouldBe("session_closed");
        ex.Reason.ShouldBe("manual_close");
        transport.SentMessages.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendTextAsync_WhenTurnCompletedArrivesBeforeTranscript_UsesGraceWindow()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(
            transport,
            new FakeTtsService(PcmBytesForMilliseconds(20)),
            settings: CreateSettings(turnCompletionTranscriptGraceMs: 80));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(2);

        transport.EnqueueInbound(TurnCompleted("provider-1"));
        await Task.Delay(20);
        transport.EnqueueInbound(AssistantTranscript("provider-1", "late transcript"));

        var response = await sendTask;

        response.Completed.ShouldBeTrue();
        response.OutputText.ShouldBe("late transcript");
    }

    [Fact]
    public async Task SendTextAsync_WhenTranscriptContinuesAfterTurnCompleted_IncludesTailTranscript()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(
            transport,
            new FakeTtsService(PcmBytesForMilliseconds(20)),
            settings: CreateSettings(turnCompletionTranscriptGraceMs: 80));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(2);

        transport.EnqueueInbound(AssistantTranscript("provider-1", "first part"));
        transport.EnqueueInbound(TurnCompleted("provider-1"));
        await Task.Delay(20);
        transport.EnqueueInbound(AssistantTranscript("provider-1", "tail part"));

        var response = await sendTask;

        response.Completed.ShouldBeTrue();
        response.OutputText.ShouldBe("first part tail part");
    }

    [Fact]
    public async Task Session_WhenNoUserTextArrivesBeforeIdleTimeout_ClosesAndSignalsIdleReason()
    {
        var closedReasons = new ConcurrentQueue<string>();
        var session = CreateSession(
            new FakeRealtimeHttpGatewayTransport(),
            new FakeTtsService(PcmBytesForMilliseconds(20)),
            settings: CreateSettings(idleTimeoutMs: 20),
            onClosed: (_, reason) => closedReasons.Enqueue(reason));

        session.StartReceiving();

        await WaitUntilAsync(() => closedReasons.TryPeek(out _));

        closedReasons.TryPeek(out var reason).ShouldBeTrue();
        reason.ShouldBe(RealtimeHttpSession.IdleCloseReason);
        session.GetDetail().Status.ShouldBe("closed");
    }

    [Fact]
    public async Task Session_DoesNotIdleCloseWhileTurnIsProcessing()
    {
        var closedReasons = new ConcurrentQueue<string>();
        var transport = new FakeRealtimeHttpGatewayTransport();
        var session = CreateSession(
            transport,
            new FakeTtsService(PcmBytesForMilliseconds(20)),
            settings: CreateSettings(idleTimeoutMs: 20),
            onClosed: (_, reason) => closedReasons.Enqueue(reason));
        session.StartReceiving();

        var sendTask = session.SendTextAsync("hello", 1000, CancellationToken.None);
        await transport.WaitForSentCountAsync(2);
        await Task.Delay(80);

        closedReasons.IsEmpty.ShouldBeTrue();

        transport.EnqueueInbound(AssistantTranscript("provider-1", "ok"));
        transport.EnqueueInbound(TurnCompleted("provider-1"));
        await sendTask;
    }

    [Fact]
    public async Task GatewayService_ReturnsNotFoundAndClosedSessionErrorsWithoutPersistence()
    {
        var transport = new FakeRealtimeHttpGatewayTransport();
        var service = CreateGatewayService(transport);

        var missing = await Should.ThrowAsync<RealtimeHttpGatewayException>(() =>
            service.SendMessageAsync("missing", new RealtimeHttpSendMessageRequest { Text = "hello" }, CancellationToken.None));
        missing.Code.ShouldBe("session_not_found");

        var created = await service.CreateSessionAsync(new RealtimeHttpCreateSessionRequest
        {
            AssistantId = 123,
            Region = RealtimeAiServerRegion.US
        }, CancellationToken.None);

        transport.EnqueueInbound(TurnCompleted("provider-closed"));
        await service.DisconnectSessionAsync(created.SessionId, "manual_close", CancellationToken.None);

        var closed = await Should.ThrowAsync<RealtimeHttpGatewayException>(() =>
            service.SendMessageAsync(created.SessionId, new RealtimeHttpSendMessageRequest { Text = "again" }, CancellationToken.None));
        closed.Code.ShouldBe("session_closed");
        closed.StatusCode.ShouldBe(HttpStatusCode.Gone);
        closed.Reason.ShouldBe("manual_close");

        var detail = await service.GetSessionAsync(created.SessionId);
        detail.ShouldNotBeNull();
        detail.Status.ShouldBe("closed");
        detail.CloseReason.ShouldBe("manual_close");
    }

    [Fact]
    public async Task RecordingInfoReader_ReadsMetadataAndTranscriptionsWithoutGatewayStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "processed"));
        Directory.CreateDirectory(Path.Combine(root, "callbacks"));
        var recordingPath = Path.Combine(root, "call.wav");
        await File.WriteAllBytesAsync(recordingPath, [1, 2, 3]);
        var providerSessionId = Guid.NewGuid().ToString();

        await File.WriteAllTextAsync(Path.Combine(root, "processed", $"{providerSessionId}.json"),
            $$"""{"RecordingUrl":"call.wav","RecordingFileSize":3,"ProcessedAt":"2026-07-16T00:00:00Z"}""");
        await File.WriteAllTextAsync(Path.Combine(root, "callbacks", $"aikid-conversation-{providerSessionId}.json"),
            """{"Transcriptions":[{"Speaker":1,"Transcription":"hello"},{"Speaker":2,"Transcription":"hi"}]}""");

        var reader = new RealtimeHttpRecordingInfoReader(CreateSettings(recordingRoot: root));
        var response = await reader.GetRecordingInfoAsync("session", providerSessionId, id => $"/download/{id}", CancellationToken.None);

        response.Ready.ShouldBeTrue();
        response.RecordingFileSize.ShouldBe(3);
        response.DownloadUrl.ShouldBe($"/download/{providerSessionId}");
        response.Transcriptions.Count.ShouldBe(2);
    }

    private static RealtimeHttpSession CreateSession(
        FakeRealtimeHttpGatewayTransport transport,
        IRealtimeHttpTtsService ttsService,
        RealtimeHttpGatewaySettings? settings = null,
        Action<RealtimeHttpSession, string>? onClosed = null)
    {
        return new RealtimeHttpSession(
            "session-1",
            123,
            RealtimeAiServerRegion.US,
            transport,
            settings ?? CreateSettings(),
            ttsService,
            onClosed ?? ((_, _) => { }));
    }

    private static RealtimeHttpGatewayService CreateGatewayService(FakeRealtimeHttpGatewayTransport transport)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Request.Scheme = "http";
        httpContextAccessor.HttpContext.Request.Host = new HostString("localhost");

        var factory = Substitute.For<IRealtimeHttpGatewayTransportFactory>();
        factory.Create().Returns(transport);

        var reader = Substitute.For<IRealtimeHttpRecordingInfoReader>();
        return new RealtimeHttpGatewayService(
            httpContextAccessor,
            CreateSettings(),
            new FakeTtsService(PcmBytesForMilliseconds(20)),
            factory,
            reader);
    }

    private static RealtimeHttpGatewaySettings CreateSettings(
        int idleTimeoutMs = 1000,
        string recordingRoot = "",
        int turnCompletionTranscriptGraceMs = 50,
        bool sendTextAsTextInput = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["RealtimeHttpGateway:IdleTimeoutMs"] = idleTimeoutMs.ToString(),
            ["RealtimeHttpGateway:UserSpeechTailSilenceMs"] = "40",
            ["RealtimeHttpGateway:TurnCompletionTranscriptGraceMs"] = turnCompletionTranscriptGraceMs.ToString(),
            ["RealtimeHttpGateway:SendTextAsTextInput"] = sendTextAsTextInput.ToString(),
            ["RealtimeHttpGateway:RealTimeAudioPacingEnabled"] = "false",
            ["RealtimeHttpGateway:DefaultResponseTimeoutMs"] = "1000",
            ["RealtimeHttpGateway:RecordingStorageBasePath"] = recordingRoot,
            ["RealtimeHttpGateway:Tts:SampleRate"] = "1000",
            ["RealtimeHttpGateway:Tts:ChunkDurationMs"] = "20",
            ["RealtimeHttpGateway:Tts:AppendSilenceMs"] = "0"
        };

        return new RealtimeHttpGatewaySettings(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    private static byte[] PcmBytesForMilliseconds(int milliseconds)
    {
        return new byte[milliseconds * 2];
    }

    private static string AssistantTranscript(string providerSessionId, string transcript)
    {
        return "{\"type\":\"OutputAudioTranscriptionCompleted\",\"session_id\":\"" + providerSessionId
               + "\",\"Data\":{\"transcriptionData\":{\"Transcript\":\"" + transcript + "\"}}}";
    }

    private static string TurnCompleted(string providerSessionId)
    {
        return "{\"type\":\"AiTurnCompleted\",\"session_id\":\"" + providerSessionId + "\"}";
    }

    private static string ResponseAudioDelta(string providerSessionId)
    {
        return "{\"type\":\"ResponseAudioDelta\",\"session_id\":\"" + providerSessionId
               + "\",\"Data\":{\"Base64Payload\":\"AQI=\"}}";
    }

    private static string ClientError(string providerSessionId, string message)
    {
        return "{\"type\":\"ClientError\",\"session_id\":\"" + providerSessionId
               + "\",\"Data\":{\"Message\":\"" + message + "\"}}";
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");

            await Task.Delay(10);
        }
    }

    private static int GetPayloadByteCount(string rawMessage)
    {
        using var doc = JsonDocument.Parse(rawMessage);
        var payload = doc.RootElement.GetProperty("media").GetProperty("payload").GetString();
        return Convert.FromBase64String(payload!).Length;
    }

    private static int GetRecordingPayloadByteCount(string rawMessage)
    {
        using var doc = JsonDocument.Parse(rawMessage);
        doc.RootElement.GetProperty("type").GetString().ShouldBe("RealtimeHttpRecordingAudio");
        var payload = doc.RootElement.GetProperty("payload").GetString();
        return Convert.FromBase64String(payload!).Length;
    }

    private static string GetTextPayload(string rawMessage)
    {
        using var doc = JsonDocument.Parse(rawMessage);
        doc.RootElement.GetProperty("type").GetString().ShouldBe("RealtimeHttpTextInput");
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }

    private sealed class FakeTtsService : IRealtimeHttpTtsService
    {
        private readonly byte[] _audioBytes;

        public FakeTtsService(byte[] audioBytes)
        {
            _audioBytes = audioBytes;
        }

        public Task<byte[]> SynthesizePcm16Async(string text, CancellationToken cancellationToken)
        {
            return Task.FromResult(_audioBytes);
        }
    }

    private sealed class BlockingTtsService : IRealtimeHttpTtsService
    {
        private readonly byte[] _audioBytes;
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingTtsService(byte[] audioBytes)
        {
            _audioBytes = audioBytes;
        }

        public async Task<byte[]> SynthesizePcm16Async(string text, CancellationToken cancellationToken)
        {
            _started.TrySetResult(true);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return _audioBytes;
        }

        public Task WaitStartedAsync()
        {
            return _started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    private sealed class FakeRealtimeHttpGatewayTransport : IRealtimeHttpGatewayTransport
    {
        private readonly Channel<string> _inbound = Channel.CreateUnbounded<string>();
        private readonly TaskCompletionSource<bool> _sentSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> SentMessages { get; } = [];

        public WebSocketState State { get; private set; } = WebSocketState.Open;

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            State = WebSocketState.Open;
            return Task.CompletedTask;
        }

        public Task SendTextAsync(string rawMessage, CancellationToken cancellationToken)
        {
            SentMessages.Add(rawMessage);
            _sentSignal.TrySetResult(true);
            return Task.CompletedTask;
        }

        public async Task<string> ReceiveTextAsync(CancellationToken cancellationToken)
        {
            return await _inbound.Reader.ReadAsync(cancellationToken);
        }

        public Task CloseAsync(string reason, CancellationToken cancellationToken)
        {
            State = WebSocketState.Closed;
            _inbound.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public void EnqueueInbound(string rawMessage)
        {
            _inbound.Writer.TryWrite(rawMessage);
        }

        public async Task WaitForSentCountAsync(int count)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (SentMessages.Count < count)
            {
                if (DateTimeOffset.UtcNow > deadline)
                    throw new TimeoutException("Expected sent message was not observed.");

                await Task.WhenAny(_sentSignal.Task, Task.Delay(10));
            }
        }

        public ValueTask DisposeAsync()
        {
            State = WebSocketState.Closed;
            _inbound.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
