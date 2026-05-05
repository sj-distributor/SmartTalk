using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiKids;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.AiKids;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.DifyRealtime;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.DifyRealtime;

public interface IDifyRealtimeBridgeService : ISingletonDependency
{
    Task<DifyRealtimeMessageResponse> SendMessageAsync(DifyRealtimeMessageRequest request, CancellationToken cancellationToken);

    Task<DifyRealtimeEndSessionResponse> EndSessionAsync(DifyRealtimeEndSessionRequest request, CancellationToken cancellationToken);
}

public class DifyRealtimeBridgeService : IDifyRealtimeBridgeService
{
    private const string DefaultVoice = "alloy";
    private const int DefaultTimeoutSeconds = 60;
    private const int DefaultEndSessionTimeoutSeconds = 120;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ConcurrentDictionary<string, DifyRealtimeSession> _sessions = new();

    public DifyRealtimeBridgeService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<DifyRealtimeMessageResponse> SendMessageAsync(DifyRealtimeMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userText = request.Query ?? request.Text;
        if (string.IsNullOrWhiteSpace(userText))
            throw new ArgumentException("Dify realtime message requires Query or Text.", nameof(request));

        if (request.AssistantId <= 0)
            throw new ArgumentException("AssistantId is required.", nameof(request));

        var sessionKey = BuildSessionKey(request.AssistantId, request.ConversationId, request.User);
        Log.Information(
            "[DifyRealtime] Incoming message, AssistantId: {AssistantId}, SessionKey: {SessionKey}, ConversationId: {ConversationId}, User: {User}, TextLength: {TextLength}, TimeoutSeconds: {TimeoutSeconds}, EndSession: {EndSession}",
            request.AssistantId,
            sessionKey,
            request.ConversationId,
            request.User,
            userText.Length,
            request.TimeoutSeconds ?? DefaultTimeoutSeconds,
            request.EndSession);

        var session = GetOrCreateSession(request);
        var ended = false;
        var recordingUrl = string.Empty;

        await session.TurnLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session.Touch();
            var drained = session.WebSocket.DrainServerMessages();
            if (drained > 0)
                Log.Debug("[DifyRealtime] Drained stale server messages, SessionId: {SessionId}, SessionKey: {SessionKey}, Count: {Count}", session.SessionId, session.Key, drained);

            session.WebSocket.EnqueueClientText(JsonSerializer.Serialize(new { text = userText }));

            var answer = await WaitForAnswerAsync(session, TimeSpan.FromSeconds(request.TimeoutSeconds ?? DefaultTimeoutSeconds), cancellationToken)
                .ConfigureAwait(false);
            session.RecordTurn(userText, answer);

            if (request.EndSession)
            {
                ended = true;
                var endResult = await RemoveAndEndSessionAsync(session.Key, cancellationToken).ConfigureAwait(false);
                recordingUrl = endResult?.RecordingUrl;
            }

            Log.Information(
                "[DifyRealtime] Message handled, SessionId: {SessionId}, SessionKey: {SessionKey}, AnswerLength: {AnswerLength}, Ended: {Ended}",
                session.SessionId,
                session.Key,
                answer?.Length ?? 0,
                ended);

            return new DifyRealtimeMessageResponse
            {
                Code = HttpStatusCode.OK,
                Data = new DifyRealtimeMessageResponseData
                {
                    SessionId = session.SessionId,
                    ConversationId = session.ConversationId,
                    Answer = answer,
                    Ended = ended,
                    RecordingUrl = recordingUrl
                }
            };
        }
        finally
        {
            session.TurnLock.Release();
        }
    }

    public async Task<DifyRealtimeEndSessionResponse> EndSessionAsync(DifyRealtimeEndSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = BuildSessionKey(request.AssistantId, request.ConversationId, request.User);
        Log.Information("[DifyRealtime] End session requested, AssistantId: {AssistantId}, SessionKey: {SessionKey}", request.AssistantId, key);
        var result = await RemoveAndEndSessionAsync(key, cancellationToken).ConfigureAwait(false);

        return new DifyRealtimeEndSessionResponse
        {
            Code = HttpStatusCode.OK,
            Data = new DifyRealtimeEndSessionResponseData
            {
                SessionId = result?.SessionId,
                ConversationId = result?.ConversationId ?? request.ConversationId,
                RecordingUrl = result?.RecordingUrl,
                Ended = true
            }
        };
    }

    private DifyRealtimeSession GetOrCreateSession(DifyRealtimeMessageRequest request)
    {
        var key = BuildSessionKey(request.AssistantId, request.ConversationId, request.User);

        if (_sessions.TryGetValue(key, out var existingSession))
        {
            Log.Debug("[DifyRealtime] Reusing existing session, SessionId: {SessionId}, SessionKey: {SessionKey}", existingSession.SessionId, key);
            return existingSession;
        }

        var newSession = CreateSession(key, request);

        if (_sessions.TryAdd(key, newSession))
        {
            Log.Information("[DifyRealtime] Created new session, SessionId: {SessionId}, SessionKey: {SessionKey}", newSession.SessionId, key);
            return newSession;
        }

        _ = newSession.EndAsync(CancellationToken.None);
        Log.Warning("[DifyRealtime] Session create race detected, discarded new session, SessionKey: {SessionKey}", key);
        return _sessions[key];
    }

    private DifyRealtimeSession CreateSession(string key, DifyRealtimeMessageRequest request)
    {
        var scope = _serviceScopeFactory.CreateScope();
        var webSocket = new DifyRealtimeWebSocket();
        var voice = ResolveRecordingVoice(request);
        var session = new DifyRealtimeSession(key, request.ConversationId, webSocket, scope, request.AssistantId, request.OrderRecordType, voice);

        var aiKidRealtimeService = scope.ServiceProvider.GetRequiredService<IAiKidRealtimeServiceV2>();

        var command = new AiKidRealtimeCommand
        {
            AssistantId = request.AssistantId,
            WebSocket = webSocket,
            Region = request.Region,
            OrderRecordType = request.OrderRecordType,
            SuppressGreeting = true,
            DisableIdleFollowUp = true,
            RecordTextInputAsTranscription = true,
            TextInputRecordingAudioProviderAsync = (text, token) => BuildTextRecordingAudioAsync(
                scope.ServiceProvider,
                text,
                voice,
                token),
            OnRecordingUploadedAsync = (providerSessionId, recordingUrl) =>
                session.SetRecordingUploadedAsync(providerSessionId, recordingUrl)
        };

        Log.Information(
            "[DifyRealtime] Starting realtime runner, SessionId: {SessionId}, SessionKey: {SessionKey}, AssistantId: {AssistantId}, Region: {Region}, OrderRecordType: {OrderRecordType}, Voice: {Voice}",
            session.SessionId,
            key,
            request.AssistantId,
            request.Region,
            request.OrderRecordType,
            voice);

        session.Runner = Task.Run(async () =>
        {
            try
            {
                await aiKidRealtimeService.RealtimeAiConnectAsync(command, CancellationToken.None).ConfigureAwait(false);
                Log.Information("[DifyRealtime] Realtime runner completed, SessionId: {SessionId}, SessionKey: {SessionKey}", session.SessionId, key);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "[DifyRealtime] Realtime session failed, Key: {Key}", key);
            }
        });

        return session;
    }

    private static async Task<RealtimeTextRecordingAudio> BuildTextRecordingAudioAsync(
        IServiceProvider serviceProvider,
        string text,
        string voice,
        CancellationToken cancellationToken)
    {
        var openaiClient = serviceProvider.GetRequiredService<IOpenaiClient>();
        var ffmpegService = serviceProvider.GetRequiredService<IFfmpegService>();

        Log.Debug("[DifyRealtime] Generating text recording audio, TextLength: {TextLength}, Voice: {Voice}", text?.Length ?? 0, voice);
        var wavBytes = await openaiClient.GenerateSpeechAsync(text, voice, cancellationToken).ConfigureAwait(false);
        if (wavBytes is not { Length: > 0 })
        {
            Log.Warning("[DifyRealtime] Empty TTS wav bytes, TextLength: {TextLength}, Voice: {Voice}", text?.Length ?? 0, voice);
            return null;
        }

        var pcm16 = await ExtractRecordingPcm16Async(ffmpegService, wavBytes, cancellationToken).ConfigureAwait(false);

        Log.Debug("[DifyRealtime] Built text recording audio, WavBytes: {WavBytes}, Pcm16Bytes: {Pcm16Bytes}", wavBytes.Length, pcm16?.Length ?? 0);
        return pcm16 is { Length: > 0 }
            ? new RealtimeTextRecordingAudio { AudioBytes = pcm16, AudioCodec = RealtimeAiAudioCodec.PCM16 }
            : null;
    }

    private static string ResolveRecordingVoice(DifyRealtimeMessageRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Voice))
            return request.Voice;

        return request.VoiceId switch
        {
            204 => "echo",
            205 => "fable",
            206 => "nova",
            207 => "onyx",
            208 => "shimmer",
            _ => DefaultVoice
        };
    }

    private static async Task<byte[]> ExtractRecordingPcm16Async(IFfmpegService ffmpegService, byte[] wavBytes, CancellationToken cancellationToken)
    {
        var pcm16 = TryExtractPcm16(wavBytes, out var sampleRate);
        if (pcm16 is { Length: > 0 } && sampleRate == 24000) return pcm16;

        if (sampleRate == 8000)
        {
            var converted = await ffmpegService.Convert8KHzWavTo24KHzWavAsync(wavBytes, cancellationToken).ConfigureAwait(false);
            pcm16 = TryExtractPcm16(converted, out sampleRate);
            if (pcm16 is { Length: > 0 } && sampleRate == 24000) return pcm16;
        }

        Log.Warning("[DifyRealtime] Unsupported TTS wav format for recording, SampleRate: {SampleRate}", sampleRate);
        return [];
    }

    private static byte[] TryExtractPcm16(byte[] wavBytes, out int sampleRate)
    {
        sampleRate = 0;

        try
        {
            using var input = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(input);
            sampleRate = reader.WaveFormat.SampleRate;

            if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat.BitsPerSample != 16)
                return [];

            using var output = new MemoryStream();
            var bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
            var frameSize = bytesPerSample * reader.WaveFormat.Channels;
            var buffer = new byte[frameSize * 2048];

            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var offset = 0; offset + frameSize <= read; offset += frameSize)
                    output.Write(buffer, offset, bytesPerSample);
            }

            return output.ToArray();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[DifyRealtime] Failed to extract PCM16 from TTS wav");
            return [];
        }
    }

    private async Task<string> WaitForAnswerAsync(DifyRealtimeSession session, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var partialAnswer = string.Empty;
        var completedAnswer = string.Empty;
        while (!linkedCts.IsCancellationRequested)
        {
            string raw;
            try
            {
                raw = await session.WebSocket.ReadServerTextAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                Log.Warning("[DifyRealtime] Waiting answer timed out, SessionId: {SessionId}, SessionKey: {SessionKey}, TimeoutSeconds: {TimeoutSeconds}", session.SessionId, session.Key, timeout.TotalSeconds);
                break;
            }
            catch (ChannelClosedException)
            {
                Log.Warning("[DifyRealtime] Server message channel closed while waiting answer, SessionId: {SessionId}, SessionKey: {SessionKey}", session.SessionId, session.Key);
                break;
            }

            if (!TryParseClientMessage(raw, out var type, out var transcript, out var speaker))
                continue;

            if (type == "AiTurnCompleted")
            {
                Log.Information("[DifyRealtime] AI turn completed, SessionId: {SessionId}, SessionKey: {SessionKey}, CompletedAnswerLength: {CompletedAnswerLength}, PartialAnswerLength: {PartialAnswerLength}",
                    session.SessionId, session.Key, completedAnswer.Length, partialAnswer.Length);
                return string.IsNullOrWhiteSpace(completedAnswer) ? partialAnswer : completedAnswer;
            }

            if (speaker != AiSpeechAssistantSpeaker.Ai)
                continue;

            if (type == "OutputAudioTranscriptionCompleted")
                completedAnswer = transcript ?? completedAnswer;
            else if (type == "OutputAudioTranscriptionPartial")
                partialAnswer = transcript ?? partialAnswer;
        }

        Log.Information("[DifyRealtime] Returning best-effort answer, SessionId: {SessionId}, SessionKey: {SessionKey}, CompletedAnswerLength: {CompletedAnswerLength}, PartialAnswerLength: {PartialAnswerLength}",
            session.SessionId, session.Key, completedAnswer.Length, partialAnswer.Length);
        return string.IsNullOrWhiteSpace(completedAnswer) ? partialAnswer : completedAnswer;
    }

    private static bool TryParseClientMessage(string raw, out string type, out string transcript, out AiSpeechAssistantSpeaker? speaker)
    {
        type = null;
        transcript = null;
        speaker = null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            if (string.IsNullOrEmpty(type)) return false;

            if (!root.TryGetProperty("Data", out var data) && !root.TryGetProperty("data", out data))
                return true;

            if (!data.TryGetProperty("transcriptionData", out var transcriptionData) &&
                !data.TryGetProperty("TranscriptionData", out transcriptionData) &&
                !data.TryGetProperty("transcription", out transcriptionData) &&
                !data.TryGetProperty("Transcription", out transcriptionData))
                return true;

            transcript = transcriptionData.TryGetProperty("Transcript", out var transcriptProp)
                ? transcriptProp.GetString()
                : transcriptionData.TryGetProperty("transcript", out transcriptProp)
                    ? transcriptProp.GetString()
                    : null;

            if (transcriptionData.TryGetProperty("Speaker", out var speakerProp) ||
                transcriptionData.TryGetProperty("speaker", out speakerProp))
            {
                if (speakerProp.ValueKind == JsonValueKind.Number && speakerProp.TryGetInt32(out var speakerValue))
                    speaker = (AiSpeechAssistantSpeaker)speakerValue;
                else if (Enum.TryParse<AiSpeechAssistantSpeaker>(speakerProp.GetString(), true, out var parsedSpeaker))
                    speaker = parsedSpeaker;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<DifyRealtimeEndSessionResult> RemoveAndEndSessionAsync(string key, CancellationToken cancellationToken)
    {
        if (!_sessions.TryRemove(key, out var session))
        {
            Log.Debug("[DifyRealtime] Session not found while ending, SessionKey: {SessionKey}", key);
            return null;
        }

        Log.Information("[DifyRealtime] Ending session, SessionId: {SessionId}, SessionKey: {SessionKey}", session.SessionId, key);
        var recordingUrl = await session.EndAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("[DifyRealtime] Session ended, SessionId: {SessionId}, SessionKey: {SessionKey}, RecordingUrl: {RecordingUrl}", session.SessionId, key, recordingUrl);

        return new DifyRealtimeEndSessionResult
        {
            SessionId = session.SessionId,
            ConversationId = session.ConversationId,
            RecordingUrl = recordingUrl
        };
    }

    private static string BuildSessionKey(int assistantId, string conversationId, string user)
    {
        var participantKey = !string.IsNullOrWhiteSpace(conversationId)
            ? conversationId.Trim()
            : !string.IsNullOrWhiteSpace(user)
                ? user.Trim()
                : "anonymous";

        return $"{assistantId}:{participantKey}";
    }

    private sealed class DifyRealtimeSession
    {
        private readonly IServiceScope _scope;
        private readonly object _recordingLock = new();
        private readonly ConcurrentQueue<(string UserText, string Answer)> _turns = new();

        public DifyRealtimeSession(
            string key,
            string conversationId,
            DifyRealtimeWebSocket webSocket,
            IServiceScope scope,
            int assistantId,
            PhoneOrderRecordType orderRecordType,
            string voice)
        {
            Key = key;
            ConversationId = conversationId;
            WebSocket = webSocket;
            _scope = scope;
            AssistantId = assistantId;
            OrderRecordType = orderRecordType;
            Voice = voice;
            SessionId = Guid.NewGuid().ToString();
            Touch();
        }

        public string Key { get; }

        public string ConversationId { get; }

        public string SessionId { get; }

        public int AssistantId { get; }

        public PhoneOrderRecordType OrderRecordType { get; }

        public string Voice { get; }

        public DifyRealtimeWebSocket WebSocket { get; }

        public SemaphoreSlim TurnLock { get; } = new(1, 1);

        public Task Runner { get; set; }

        public DateTimeOffset LastAccessedAt { get; private set; }

        public string ProviderSessionId { get; private set; }

        public string RecordingUrl { get; private set; }

        public void Touch()
        {
            LastAccessedAt = DateTimeOffset.UtcNow;
        }

        public void RecordTurn(string userText, string answer)
        {
            if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(answer)) return;

            _turns.Enqueue((userText, answer));
        }

        public Task SetRecordingUploadedAsync(string providerSessionId, string recordingUrl)
        {
            lock (_recordingLock)
            {
                ProviderSessionId = providerSessionId;
                RecordingUrl = recordingUrl;
            }

            Log.Information(
                "[DifyRealtime] Recording uploaded, SessionId: {SessionId}, ProviderSessionId: {ProviderSessionId}, RecordingUrl: {RecordingUrl}",
                SessionId,
                providerSessionId,
                recordingUrl);

            return Task.CompletedTask;
        }

        public async Task<string> EndAsync(CancellationToken cancellationToken)
        {
            try
            {
                Log.Debug("[DifyRealtime] Sending client-close to realtime runner, SessionId: {SessionId}, SessionKey: {SessionKey}", SessionId, Key);
                WebSocket.EnqueueClientClose();

                if (Runner != null)
                    await Runner.WaitAsync(TimeSpan.FromSeconds(DefaultEndSessionTimeoutSeconds), cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Log.Warning("[DifyRealtime] Realtime runner close timeout, abort websocket, SessionId: {SessionId}, SessionKey: {SessionKey}", SessionId, Key);
                WebSocket.Abort();
            }
            finally
            {
                if (string.IsNullOrEmpty(GetRecordingUrl()))
                    await TryUploadFallbackRecordingAsync(cancellationToken).ConfigureAwait(false);

                WebSocket.Dispose();
                _scope.Dispose();
                Log.Debug("[DifyRealtime] Session resources disposed, SessionId: {SessionId}, SessionKey: {SessionKey}", SessionId, Key);
            }

            return GetRecordingUrl();
        }

        private string GetRecordingUrl()
        {
            lock (_recordingLock)
                return RecordingUrl;
        }

        private async Task TryUploadFallbackRecordingAsync(CancellationToken cancellationToken)
        {
            if (_turns.IsEmpty)
            {
                Log.Warning("[DifyRealtime] Fallback recording skipped because no text turns were captured, SessionId: {SessionId}", SessionId);
                return;
            }

            try
            {
                var fallbackText = string.Join("\n", _turns
                    .SelectMany(turn => new[] { turn.UserText, turn.Answer })
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

                if (string.IsNullOrWhiteSpace(fallbackText))
                {
                    Log.Warning("[DifyRealtime] Fallback recording skipped because captured text is empty, SessionId: {SessionId}", SessionId);
                    return;
                }

                var openaiClient = _scope.ServiceProvider.GetRequiredService<IOpenaiClient>();
                var attachmentService = _scope.ServiceProvider.GetRequiredService<IAttachmentService>();
                var backgroundJobClient = _scope.ServiceProvider.GetRequiredService<ISmartTalkBackgroundJobClient>();

                Log.Warning("[DifyRealtime] Realtime recording missing, generating fallback TTS recording, SessionId: {SessionId}, TextLength: {TextLength}", SessionId, fallbackText.Length);
                var wavBytes = await openaiClient.GenerateSpeechAsync(fallbackText, Voice, cancellationToken).ConfigureAwait(false);
                if (wavBytes is not { Length: > 0 })
                {
                    Log.Warning("[DifyRealtime] Fallback TTS returned empty audio, SessionId: {SessionId}", SessionId);
                    return;
                }

                var audio = await attachmentService.UploadAttachmentAsync(
                    new UploadAttachmentCommand
                    {
                        Attachment = new UploadAttachmentDto
                        {
                            FileName = Guid.NewGuid() + ".wav",
                            FileContent = wavBytes
                        }
                    }, CancellationToken.None).ConfigureAwait(false);

                var recordingUrl = audio?.Attachment?.FileUrl;
                if (string.IsNullOrEmpty(recordingUrl))
                {
                    Log.Warning("[DifyRealtime] Fallback recording upload returned empty url, SessionId: {SessionId}", SessionId);
                    return;
                }

                await SetRecordingUploadedAsync(SessionId, recordingUrl).ConfigureAwait(false);

                if (AssistantId != 0)
                {
                    var jobId = backgroundJobClient.Enqueue<IAiKidRealtimeProcessJobService>(x =>
                        x.RecordingRealtimeAiAsync(recordingUrl, AssistantId, SessionId, OrderRecordType, CancellationToken.None));

                    Log.Information(
                        "[DifyRealtime] Fallback recording job enqueued, SessionId: {SessionId}, AssistantId: {AssistantId}, JobId: {JobId}, RecordingUrl: {RecordingUrl}",
                        SessionId,
                        AssistantId,
                        jobId,
                        recordingUrl);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DifyRealtime] Failed to upload fallback recording, SessionId: {SessionId}", SessionId);
            }
        }
    }

    private sealed class DifyRealtimeEndSessionResult
    {
        public string SessionId { get; set; }

        public string ConversationId { get; set; }

        public string RecordingUrl { get; set; }
    }
}
