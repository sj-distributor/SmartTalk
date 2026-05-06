using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.RealtimeHttp;
using SmartTalk.Messages.Dto.RealtimeHttp;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeHttp;

public interface IRealtimeHttpGatewayService : ISingletonDependency
{
    Task<RealtimeHttpCreateSessionResponse> CreateSessionAsync(
        RealtimeHttpCreateSessionRequest request,
        CancellationToken cancellationToken);

    Task<RealtimeHttpSendMessageResponse> SendMessageAsync(
        string sessionId,
        RealtimeHttpSendMessageRequest request,
        CancellationToken cancellationToken);

    Task<RealtimeHttpRunDefaultConversationResponse> RunDefaultConversationAsync(
        RealtimeHttpRunDefaultConversationRequest request,
        CancellationToken cancellationToken);

    Task<RealtimeHttpRecordingInfoResponse> GetRecordingInfoAsync(
        string sessionIdOrProviderSessionId,
        CancellationToken cancellationToken);

    Task<RealtimeHttpSessionDetailResponse?> GetSessionAsync(string sessionId);

    IReadOnlyList<RealtimeHttpSessionDetailResponse> ListSessions();

    Task<RealtimeHttpDisconnectResponse> DisconnectSessionAsync(string sessionId, string reason, CancellationToken cancellationToken);
}

public class RealtimeHttpGatewayService : IRealtimeHttpGatewayService
{
    private readonly ConcurrentDictionary<string, GatewaySession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _closedSessionProviderMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RealtimeHttpGatewaySettings _settings;
    private readonly IRealtimeHttpTtsService _ttsService;

    public RealtimeHttpGatewayService(
        IHttpContextAccessor httpContextAccessor,
        RealtimeHttpGatewaySettings settings,
        IRealtimeHttpTtsService ttsService)
    {
        _httpContextAccessor = httpContextAccessor;
        _settings = settings;
        _ttsService = ttsService;
    }

    public async Task<RealtimeHttpCreateSessionResponse> CreateSessionAsync(
        RealtimeHttpCreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AssistantId <= 0) throw new ArgumentException("assistantId must be greater than 0.");

        var sessionId = Guid.NewGuid().ToString("N");
        var wsUri = BuildInternalWsUri(request.AssistantId, request.Region);

        var socket = new ClientWebSocket();
        await socket.ConnectAsync(wsUri, cancellationToken).ConfigureAwait(false);

        var session = new GatewaySession(
            sessionId: sessionId,
            assistantId: request.AssistantId,
            region: request.Region,
            socket: socket,
            settings: _settings,
            ttsService: _ttsService);

        if (!_sessions.TryAdd(sessionId, session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Failed to register realtime HTTP session.");
        }

        session.StartReceiving(() =>
        {
            if (!string.IsNullOrWhiteSpace(session.ProviderSessionId))
                _closedSessionProviderMap[sessionId] = session.ProviderSessionId;

            _sessions.TryRemove(sessionId, out _);
        });

        Log.Information(
            "[RealtimeHttpGateway] Session created. SessionId: {SessionId}, AssistantId: {AssistantId}, Region: {Region}, WsUri: {WsUri}",
            sessionId, request.AssistantId, request.Region, wsUri);

        return session.GetSnapshot();
    }

    public async Task<RealtimeHttpSendMessageResponse> SendMessageAsync(
        string sessionId,
        RealtimeHttpSendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("text cannot be empty.");

        var session = GetSessionOrThrow(sessionId);

        return await session.SendTextAsync(request.Text, request.TimeoutMs, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RealtimeHttpRunDefaultConversationResponse> RunDefaultConversationAsync(
        RealtimeHttpRunDefaultConversationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_settings.ScriptedConversation.Enabled)
            throw new InvalidOperationException("Scripted default conversation is disabled.");

        var prompts = _settings.ScriptedConversation.DefaultPrompts
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(2)
            .ToList();

        if (prompts.Count < 2)
            throw new InvalidOperationException("Scripted default conversation requires at least two prompts.");

        var startedAt = DateTimeOffset.UtcNow;
        var createSession = await CreateSessionAsync(new RealtimeHttpCreateSessionRequest
        {
            AssistantId = request.AssistantId,
            Region = request.Region
        }, cancellationToken).ConfigureAwait(false);

        var session = GetSessionOrThrow(createSession.SessionId);
        var scriptedTurns = new List<RealtimeHttpConversationTurnResponse>(capacity: prompts.Count);
        var warmupTurnCompleted = false;
        var warmupTurnNumber = 0;
        var closeReason = string.IsNullOrWhiteSpace(_settings.ScriptedConversation.AutoDisconnectReason)
            ? "scripted_two_turns_completed"
            : _settings.ScriptedConversation.AutoDisconnectReason;
        var disconnected = false;

        try
        {
            var warmupTimeoutMs = Math.Max(0, _settings.ScriptedConversation.WarmupWaitTimeoutMs);

            if (warmupTimeoutMs > 0)
            {
                var warmup = await session.WaitForNextTurnAsync(warmupTimeoutMs, cancellationToken).ConfigureAwait(false);
                warmupTurnCompleted = warmup.Completed;
                warmupTurnNumber = warmup.TurnNumber;
            }

            var messageTimeoutMs = Math.Max(1000, _settings.ScriptedConversation.MessageTimeoutMs);
            var maxExtraTurnWaits = Math.Max(0, _settings.ScriptedConversation.MaxExtraTurnWaitsWhenOutputEmpty);

            for (var i = 0; i < prompts.Count; i++)
            {
                var send = await SendTextAndWaitForNonEmptyOutputAsync(
                    session,
                    prompts[i],
                    messageTimeoutMs,
                    maxExtraTurnWaits,
                    cancellationToken).ConfigureAwait(false);

                scriptedTurns.Add(new RealtimeHttpConversationTurnResponse
                {
                    Index = i + 1,
                    InputText = prompts[i],
                    OutputText = send.OutputText,
                    Completed = send.Completed,
                    TurnNumber = send.TurnNumber,
                    CreatedAt = send.CreatedAt
                });

                if (!send.Completed) break;
            }

            var disconnect = await DisconnectSessionAsync(createSession.SessionId, closeReason, cancellationToken).ConfigureAwait(false);
            disconnected = disconnect.Closed;

            return new RealtimeHttpRunDefaultConversationResponse
            {
                SessionId = createSession.SessionId,
                ProviderSessionId = string.IsNullOrWhiteSpace(disconnect.ProviderSessionId) ? session.ProviderSessionId : disconnect.ProviderSessionId,
                AssistantId = createSession.AssistantId,
                Region = createSession.Region,
                WarmupTurnCompleted = warmupTurnCompleted,
                WarmupTurnNumber = warmupTurnNumber,
                Turns = scriptedTurns,
                Closed = disconnect.Closed,
                CloseReason = disconnect.Reason,
                StartedAt = startedAt,
                EndedAt = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            if (!disconnected)
            {
                try
                {
                    await DisconnectSessionAsync(createSession.SessionId, "scripted_two_turns_failed", CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[RealtimeHttpGateway] Failed to cleanup scripted session, SessionId: {SessionId}", createSession.SessionId);
                }
            }
        }
    }

    public async Task<RealtimeHttpRecordingInfoResponse> GetRecordingInfoAsync(
        string sessionIdOrProviderSessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionIdOrProviderSessionId))
            throw new ArgumentException("sessionId cannot be empty.");

        var providerSessionId = ResolveProviderSessionId(sessionIdOrProviderSessionId);
        var response = new RealtimeHttpRecordingInfoResponse
        {
            SessionId = sessionIdOrProviderSessionId,
            ProviderSessionId = providerSessionId
        };

        if (string.IsNullOrWhiteSpace(providerSessionId))
        {
            response.Ready = false;
            response.Message = "provider_session_not_resolved";
            return response;
        }

        var metadata = await TryLoadRecordingMetadataAsync(providerSessionId, cancellationToken).ConfigureAwait(false);
        if (metadata == null)
        {
            response.Ready = false;
            response.Message = "recording_metadata_not_ready";
            return response;
        }

        response.ProcessedAt = metadata.ProcessedAt;
        response.Transcriptions = await TryLoadTranscriptionsAsync(providerSessionId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(metadata.RecordingPath))
        {
            response.Ready = false;
            response.Message = "recording_path_empty";
            return response;
        }

        response.RecordingPath = metadata.RecordingPath;
        response.RecordingFileName = Path.GetFileName(metadata.RecordingPath);
        response.RecordingFileSize = metadata.RecordingFileSize;

        if (!File.Exists(metadata.RecordingPath))
        {
            response.Ready = false;
            response.Message = "recording_file_not_found";
            return response;
        }

        if (response.RecordingFileSize <= 0)
            response.RecordingFileSize = new FileInfo(metadata.RecordingPath).Length;

        response.Ready = true;
        response.Message = "ok";
        response.DownloadUrl = BuildRecordingDownloadUrl(providerSessionId);
        return response;
    }

    public Task<RealtimeHttpSessionDetailResponse?> GetSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult<RealtimeHttpSessionDetailResponse?>(null);

        return Task.FromResult<RealtimeHttpSessionDetailResponse?>(session.GetDetail());
    }

    public IReadOnlyList<RealtimeHttpSessionDetailResponse> ListSessions()
    {
        return _sessions.Values
            .Select(x => x.GetDetail())
            .OrderByDescending(x => x.LastActivityAt)
            .ToList();
    }

    public async Task<RealtimeHttpDisconnectResponse> DisconnectSessionAsync(string sessionId, string reason, CancellationToken cancellationToken)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return new RealtimeHttpDisconnectResponse
            {
                SessionId = sessionId,
                ProviderSessionId = _closedSessionProviderMap.TryGetValue(sessionId, out var mappedProvider) ? mappedProvider : string.Empty,
                Closed = false,
                Reason = "session_not_found"
            };
        }

        var providerSessionId = session.ProviderSessionId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(providerSessionId))
            _closedSessionProviderMap[sessionId] = providerSessionId;

        await session.CloseAsync(reason, cancellationToken).ConfigureAwait(false);

        return new RealtimeHttpDisconnectResponse
        {
            SessionId = sessionId,
            ProviderSessionId = providerSessionId,
            Closed = true,
            Reason = reason
        };
    }

    private Uri BuildInternalWsUri(int assistantId, RealtimeAiServerRegion region)
    {
        var httpContext = RequireHttpContext();
        var baseUrl = _settings.InternalWsBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var scheme = string.Equals(httpContext.Request.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            baseUrl = $"{scheme}://{httpContext.Request.Host.Value}";
        }
        else
        {
            if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "wss://" + baseUrl["https://".Length..];
            else if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "ws://" + baseUrl["http://".Length..];
        }

        baseUrl = baseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}/api/RealtimeAi/connect/{assistantId}/{region}");
    }

    private GatewaySession GetSessionOrThrow(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            return session;

        throw new KeyNotFoundException($"Session not found: {sessionId}");
    }

    private static async Task<RealtimeHttpSendMessageResponse> SendTextAndWaitForNonEmptyOutputAsync(
        GatewaySession session,
        string inputText,
        int messageTimeoutMs,
        int maxExtraTurnWaits,
        CancellationToken cancellationToken)
    {
        var send = await session.SendTextAsync(inputText, messageTimeoutMs, cancellationToken).ConfigureAwait(false);
        if (!send.Completed) return send;
        if (!string.IsNullOrWhiteSpace(send.OutputText)) return send;

        for (var i = 0; i < maxExtraTurnWaits; i++)
        {
            var wait = await session.WaitForNextTurnAsync(messageTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (!wait.Completed) break;

            if (!string.IsNullOrWhiteSpace(wait.OutputText))
            {
                return new RealtimeHttpSendMessageResponse
                {
                    SessionId = send.SessionId,
                    InputText = inputText,
                    OutputText = wait.OutputText,
                    Completed = true,
                    TurnNumber = wait.TurnNumber,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }
        }

        return send;
    }

    private string ResolveProviderSessionId(string sessionIdOrProviderSessionId)
    {
        if (_sessions.TryGetValue(sessionIdOrProviderSessionId, out var activeSession)
            && !string.IsNullOrWhiteSpace(activeSession.ProviderSessionId))
        {
            return activeSession.ProviderSessionId;
        }

        if (_closedSessionProviderMap.TryGetValue(sessionIdOrProviderSessionId, out var mappedProviderSessionId))
            return mappedProviderSessionId;

        return Guid.TryParse(sessionIdOrProviderSessionId, out _) ? sessionIdOrProviderSessionId : string.Empty;
    }

    private async Task<RecordingMetadata?> TryLoadRecordingMetadataAsync(string providerSessionId, CancellationToken cancellationToken)
    {
        var storageRoot = GetRecordingStorageRoot();
        if (string.IsNullOrWhiteSpace(storageRoot))
            return null;

        var metadataPath = Path.Combine(storageRoot, _settings.RecordingProcessedFolder, $"{providerSessionId}.json");
        if (!File.Exists(metadataPath))
            return null;

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var rawRecordingPath = TryReadString(root, "RecordingUrl");
        var normalizedRecordingPath = NormalizeRecordingPath(rawRecordingPath, storageRoot);
        var recordingFileSize = TryReadLong(root, "RecordingFileSize");
        var recordingFileExists = TryReadBool(root, "RecordingFileExists");
        var processedAt = TryReadDateTimeOffset(root, "ProcessedAt");

        if (recordingFileSize <= 0 && !string.IsNullOrWhiteSpace(normalizedRecordingPath) && File.Exists(normalizedRecordingPath))
            recordingFileSize = new FileInfo(normalizedRecordingPath).Length;

        return new RecordingMetadata
        {
            RecordingPath = normalizedRecordingPath,
            RecordingFileSize = recordingFileSize,
            RecordingFileExists = recordingFileExists,
            ProcessedAt = processedAt
        };
    }

    private async Task<List<RealtimeHttpTranscriptionItemDto>> TryLoadTranscriptionsAsync(string providerSessionId, CancellationToken cancellationToken)
    {
        var storageRoot = GetRecordingStorageRoot();
        if (string.IsNullOrWhiteSpace(storageRoot))
            return [];

        var callbackPath = Path.Combine(storageRoot, _settings.RecordingCallbackFolder, $"aikid-conversation-{providerSessionId}.json");
        if (!File.Exists(callbackPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(callbackPath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Transcriptions", out var transcriptions)
                || transcriptions.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<RealtimeHttpTranscriptionItemDto>();
            foreach (var item in transcriptions.EnumerateArray())
            {
                var transcription = TryReadString(item, "Transcription");
                if (string.IsNullOrWhiteSpace(transcription))
                    continue;

                result.Add(new RealtimeHttpTranscriptionItemDto
                {
                    Speaker = TryReadInt(item, "Speaker"),
                    Transcription = transcription
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeHttpGateway] Failed to parse transcription callback, ProviderSessionId: {ProviderSessionId}", providerSessionId);
            return [];
        }
    }

    private string GetRecordingStorageRoot()
    {
        if (string.IsNullOrWhiteSpace(_settings.RecordingStorageBasePath))
            return string.Empty;

        try
        {
            return Path.GetFullPath(_settings.RecordingStorageBasePath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildRecordingDownloadUrl(string providerSessionId)
    {
        var httpContext = RequireHttpContext();
        var escaped = Uri.EscapeDataString(providerSessionId);
        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/RealtimeHttpGateway/recordings/{escaped}/file";
    }

    private HttpContext RequireHttpContext()
    {
        return _httpContextAccessor.HttpContext
               ?? throw new InvalidOperationException("Current HttpContext is not available.");
    }

    private static string NormalizeRecordingPath(string rawPath, string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        var fullPath = Path.GetFullPath(Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(storageRoot, rawPath));
        var normalizedStorageRoot = Path.GetFullPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;
        var normalizedFullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!normalizedFullPath.StartsWith(normalizedStorageRoot, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return fullPath;
    }

    private static string TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

    private static long TryReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static int TryReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static bool TryReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static DateTimeOffset? TryReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed class RecordingMetadata
    {
        public string RecordingPath { get; init; } = string.Empty;

        public long RecordingFileSize { get; init; }

        public bool RecordingFileExists { get; init; }

        public DateTimeOffset? ProcessedAt { get; init; }
    }

    private sealed class GatewaySession : IAsyncDisposable
    {
        private readonly string _sessionId;
        private readonly int _assistantId;
        private readonly RealtimeAiServerRegion _region;
        private readonly ClientWebSocket _socket;
        private readonly RealtimeHttpGatewaySettings _settings;
        private readonly IRealtimeHttpTtsService _ttsService;

        private readonly CancellationTokenSource _sessionCts = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly object _stateLock = new();

        private readonly List<string> _activeAssistantTranscripts = [];
        private readonly List<RealtimeHttpSessionEventDto> _recentEvents = [];

        private int _completedTurns;
        private string _status = "connected";
        private string _lastError = string.Empty;
        private string _providerSessionId = string.Empty;
        private long _eventSequence;

        private TaskCompletionSource<(int TurnNumber, string OutputText)>? _pendingTurnAwaiter;
        private int _pendingAwaiterTargetTurn;

        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastActivityAt { get; private set; } = DateTimeOffset.UtcNow;

        public string ProviderSessionId
        {
            get
            {
                lock (_stateLock)
                {
                    return _providerSessionId;
                }
            }
        }

        private Task? _receiveLoopTask;

        public GatewaySession(
            string sessionId,
            int assistantId,
            RealtimeAiServerRegion region,
            ClientWebSocket socket,
            RealtimeHttpGatewaySettings settings,
            IRealtimeHttpTtsService ttsService)
        {
            _sessionId = sessionId;
            _assistantId = assistantId;
            _region = region;
            _socket = socket;
            _settings = settings;
            _ttsService = ttsService;
        }

        public void StartReceiving(Action onLoopEnded)
        {
            _receiveLoopTask = Task.Run(async () =>
            {
                try
                {
                    await ReceiveLoopAsync(_sessionCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    onLoopEnded();
                }
            });
        }

        public RealtimeHttpCreateSessionResponse GetSnapshot()
        {
            return new RealtimeHttpCreateSessionResponse
            {
                SessionId = _sessionId,
                ProviderSessionId = ProviderSessionId ?? string.Empty,
                AssistantId = _assistantId,
                Region = _region,
                Status = _status,
                CreatedAt = CreatedAt
            };
        }

        public RealtimeHttpSessionDetailResponse GetDetail()
        {
            lock (_stateLock)
            {
                return new RealtimeHttpSessionDetailResponse
                {
                    SessionId = _sessionId,
                    ProviderSessionId = ProviderSessionId ?? string.Empty,
                    AssistantId = _assistantId,
                    Region = _region,
                    Status = _status,
                    CreatedAt = CreatedAt,
                    LastActivityAt = LastActivityAt,
                    LastError = _lastError,
                    CompletedTurns = _completedTurns,
                    RecentEvents = _recentEvents.ToList()
                };
            }
        }

        public async Task<RealtimeHttpSendMessageResponse> SendTextAsync(string inputText, int? timeoutMs, CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureConnected();

                var effectiveTimeoutMs = timeoutMs ?? _settings.DefaultResponseTimeoutMs;
                var audioBytes = await _ttsService.SynthesizePcm16Async(inputText, cancellationToken).ConfigureAwait(false);

                var waitTask = PrepareTurnAwaiter();

                AppendEvent("user_text_input", "user", inputText);

                await SendAudioChunksAsync(audioBytes, cancellationToken).ConfigureAwait(false);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, effectiveTimeoutMs)));

                try
                {
                    var (turnNumber, outputText) = await waitTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                    return new RealtimeHttpSendMessageResponse
                    {
                        SessionId = _sessionId,
                        InputText = inputText,
                        OutputText = outputText,
                        Completed = true,
                        TurnNumber = turnNumber,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                }
                catch (OperationCanceledException)
                {
                    return new RealtimeHttpSendMessageResponse
                    {
                        SessionId = _sessionId,
                        InputText = inputText,
                        OutputText = string.Empty,
                        Completed = false,
                        TurnNumber = 0,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                }
            }
            finally
            {
                ClearTurnAwaiter();
                _sendLock.Release();
            }
        }

        public async Task<GatewayTurnWaitResult> WaitForNextTurnAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureConnected();

                var waitTask = PrepareTurnAwaiter();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, timeoutMs)));

                try
                {
                    var (turnNumber, outputText) = await waitTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                    return new GatewayTurnWaitResult
                    {
                        Completed = true,
                        TurnNumber = turnNumber,
                        OutputText = outputText
                    };
                }
                catch (OperationCanceledException)
                {
                    return new GatewayTurnWaitResult
                    {
                        Completed = false,
                        TurnNumber = 0,
                        OutputText = string.Empty
                    };
                }
            }
            finally
            {
                ClearTurnAwaiter();
                _sendLock.Release();
            }
        }

        public async Task CloseAsync(string reason, CancellationToken cancellationToken)
        {
            lock (_stateLock)
            {
                _status = "closing";
            }

            _sessionCts.Cancel();

            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[RealtimeHttpGateway] CloseOutputAsync failed, SessionId: {SessionId}", _sessionId);
                }
            }

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore loop wait timeout.
                }
            }

            await DisposeAsync().ConfigureAwait(false);
        }

        private async Task SendAudioChunksAsync(byte[] pcmBytes, CancellationToken cancellationToken)
        {
            if (pcmBytes.Length == 0) return;

            var bytesPerChunk = Math.Max(
                480,
                _settings.Tts.SampleRate * Math.Max(10, _settings.Tts.ChunkDurationMs) / 1000 * 2);
            var chunkIntervalMs = Math.Max(5, _settings.Tts.ChunkDurationMs);

            for (var offset = 0; offset < pcmBytes.Length; offset += bytesPerChunk)
            {
                var count = Math.Min(bytesPerChunk, pcmBytes.Length - offset);
                var slice = new byte[count];
                Buffer.BlockCopy(pcmBytes, offset, slice, 0, count);

                var payload = Convert.ToBase64String(slice);
                var message = JsonSerializer.Serialize(new
                {
                    media = new
                    {
                        type = "audio",
                        payload
                    }
                });

                await SendRawAsync(message, cancellationToken).ConfigureAwait(false);
                await Task.Delay(chunkIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        private Task<(int TurnNumber, string OutputText)> PrepareTurnAwaiter()
        {
            lock (_stateLock)
            {
                _pendingAwaiterTargetTurn = _completedTurns + 1;
                _pendingTurnAwaiter = new TaskCompletionSource<(int TurnNumber, string OutputText)>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _pendingTurnAwaiter.Task;
            }
        }

        private void ClearTurnAwaiter()
        {
            lock (_stateLock)
            {
                _pendingTurnAwaiter = null;
                _pendingAwaiterTargetTurn = 0;
            }
        }

        private async Task SendRawAsync(string rawMessage, CancellationToken cancellationToken)
        {
            EnsureConnected();

            var bytes = Encoding.UTF8.GetBytes(rawMessage);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            LastActivityAt = DateTimeOffset.UtcNow;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            try
            {
                while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    ValueWebSocketReceiveResult receive;

                    do
                    {
                        receive = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

                        if (receive.MessageType == WebSocketMessageType.Close)
                        {
                            lock (_stateLock)
                            {
                                _status = "closed";
                            }
                            CompleteAwaiterIfNeeded();
                            return;
                        }

                        ms.Write(buffer, 0, receive.Count);
                    } while (!receive.EndOfMessage);

                    var rawMessage = Encoding.UTF8.GetString(ms.ToArray());
                    LastActivityAt = DateTimeOffset.UtcNow;
                    HandleInboundMessage(rawMessage);
                }
            }
            catch (OperationCanceledException)
            {
                lock (_stateLock)
                {
                    if (_status != "closed") _status = "cancelled";
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RealtimeHttpGateway] Receive loop error, SessionId: {SessionId}", _sessionId);
                lock (_stateLock)
                {
                    _status = "faulted";
                    _lastError = ex.Message;
                }
                AppendEvent("gateway_error", "system", ex.Message);
            }
            finally
            {
                CompleteAwaiterIfNeeded();
            }
        }

        private void HandleInboundMessage(string rawMessage)
        {
            if (!TryGetRoot(rawMessage, out var root)) return;

            if (TryGetStringProperty(root, "session_id", out var providerSessionId) && !string.IsNullOrWhiteSpace(providerSessionId))
            {
                lock (_stateLock)
                {
                    _providerSessionId = providerSessionId;
                }
            }

            if (!TryGetStringProperty(root, "type", out var type)) return;

            switch (type)
            {
                case "OutputAudioTranscriptionCompleted":
                    var outputTranscript = ExtractTranscript(root);
                    if (!string.IsNullOrWhiteSpace(outputTranscript))
                    {
                        lock (_stateLock)
                        {
                            _activeAssistantTranscripts.Add(outputTranscript);
                        }
                        AppendEvent("assistant_transcript_completed", "assistant", outputTranscript);
                    }
                    break;

                case "InputAudioTranscriptionCompleted":
                    var inputTranscript = ExtractTranscript(root);
                    if (!string.IsNullOrWhiteSpace(inputTranscript))
                        AppendEvent("user_transcript_completed", "user", inputTranscript);
                    break;

                case "AiTurnCompleted":
                    CompleteTurn();
                    break;

                case "ClientError":
                    var err = ExtractClientError(root);
                    lock (_stateLock)
                    {
                        _lastError = err;
                    }
                    AppendEvent("provider_error", "system", err);
                    break;
            }
        }

        private void CompleteTurn()
        {
            string outputText;
            int turnNumber;
            TaskCompletionSource<(int TurnNumber, string OutputText)>? pending = null;
            var triggerAwaiter = false;

            lock (_stateLock)
            {
                turnNumber = ++_completedTurns;
                outputText = string.Join(" ", _activeAssistantTranscripts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
                _activeAssistantTranscripts.Clear();

                if (_pendingTurnAwaiter != null && turnNumber >= _pendingAwaiterTargetTurn)
                {
                    pending = _pendingTurnAwaiter;
                    triggerAwaiter = true;
                }
            }

            AppendEvent("assistant_turn_completed", "assistant", outputText);

            if (triggerAwaiter)
                pending?.TrySetResult((turnNumber, outputText));
        }

        private void CompleteAwaiterIfNeeded()
        {
            TaskCompletionSource<(int TurnNumber, string OutputText)>? pending = null;
            lock (_stateLock)
            {
                pending = _pendingTurnAwaiter;
            }

            pending?.TrySetCanceled();
        }

        private void AppendEvent(string type, string role, string content)
        {
            var evt = new RealtimeHttpSessionEventDto
            {
                Sequence = Interlocked.Increment(ref _eventSequence),
                Type = type,
                Role = role,
                Content = content,
                CreatedAt = DateTimeOffset.UtcNow
            };

            lock (_stateLock)
            {
                _recentEvents.Add(evt);

                while (_recentEvents.Count > Math.Max(20, _settings.RecentEventCapacity))
                    _recentEvents.RemoveAt(0);
            }
        }

        private void EnsureConnected()
        {
            if (_socket.State != WebSocketState.Open)
                throw new InvalidOperationException($"Session {_sessionId} is not open. Current state: {_socket.State}");
        }

        private static bool TryGetRoot(string rawMessage, out JsonElement root)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawMessage);
                root = doc.RootElement.Clone();
                return true;
            }
            catch
            {
                root = default;
                return false;
            }
        }

        private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
        {
            if (TryGetPropertyIgnoreCase(element, propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString() ?? string.Empty;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static string ExtractTranscript(JsonElement root)
        {
            if (!TryGetPropertyIgnoreCase(root, "Data", out var data)) return string.Empty;
            if (!TryGetPropertyIgnoreCase(data, "transcriptionData", out var transcriptionData)) return string.Empty;
            return TryGetStringProperty(transcriptionData, "Transcript", out var transcript) ? transcript : string.Empty;
        }

        private static string ExtractClientError(JsonElement root)
        {
            if (!TryGetPropertyIgnoreCase(root, "Data", out var data)) return "unknown_error";
            return TryGetStringProperty(data, "Message", out var message) ? message : "unknown_error";
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _sessionCts.Cancel();
            }
            catch
            {
                // ignored
            }

            _sessionCts.Dispose();
            _sendLock.Dispose();

            try
            {
                _socket.Dispose();
            }
            catch
            {
                // ignored
            }

            await Task.CompletedTask;
        }

        public class GatewayTurnWaitResult
        {
            public bool Completed { get; init; }

            public int TurnNumber { get; init; }

            public string OutputText { get; init; } = string.Empty;
        }
    }
}
