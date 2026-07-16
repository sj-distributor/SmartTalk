using System.Collections.Concurrent;
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

    Task<RealtimeHttpSessionDetailResponse> GetSessionAsync(string sessionId);

    IReadOnlyList<RealtimeHttpSessionDetailResponse> ListSessions();

    Task<RealtimeHttpDisconnectResponse> DisconnectSessionAsync(string sessionId, string reason, CancellationToken cancellationToken);
}

public sealed class RealtimeHttpGatewayService : IRealtimeHttpGatewayService
{
    private readonly ConcurrentDictionary<string, RealtimeHttpSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ClosedSessionInfo> _closedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RealtimeHttpGatewaySettings _settings;
    private readonly IRealtimeHttpTtsService _ttsService;
    private readonly IRealtimeHttpGatewayTransportFactory _transportFactory;
    private readonly IRealtimeHttpRecordingInfoReader _recordingInfoReader;

    public RealtimeHttpGatewayService(
        IHttpContextAccessor httpContextAccessor,
        RealtimeHttpGatewaySettings settings,
        IRealtimeHttpTtsService ttsService,
        IRealtimeHttpGatewayTransportFactory transportFactory,
        IRealtimeHttpRecordingInfoReader recordingInfoReader)
    {
        _httpContextAccessor = httpContextAccessor;
        _settings = settings;
        _ttsService = ttsService;
        _transportFactory = transportFactory;
        _recordingInfoReader = recordingInfoReader;
    }

    public async Task<RealtimeHttpCreateSessionResponse> CreateSessionAsync(
        RealtimeHttpCreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AssistantId <= 0) throw new ArgumentException("assistantId must be greater than 0.");

        PruneClosedSessions();

        var sessionId = Guid.NewGuid().ToString("N");
        var wsUri = BuildInternalWsUri(request.AssistantId, request.Region);
        var transport = _transportFactory.Create();
        await transport.ConnectAsync(wsUri, cancellationToken).ConfigureAwait(false);

        var session = new RealtimeHttpSession(
            sessionId,
            request.AssistantId,
            request.Region,
            transport,
            _settings,
            _ttsService,
            StoreClosedSession);

        if (!_sessions.TryAdd(sessionId, session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Failed to register realtime HTTP session.");
        }

        session.StartReceiving();

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

        var session = GetActiveSessionOrThrow(sessionId);
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

        var scriptedTurns = new List<RealtimeHttpConversationTurnResponse>(capacity: prompts.Count);
        var closeReason = string.IsNullOrWhiteSpace(_settings.ScriptedConversation.AutoDisconnectReason)
            ? "scripted_two_turns_completed"
            : _settings.ScriptedConversation.AutoDisconnectReason;
        var disconnected = false;

        try
        {
            var messageTimeoutMs = Math.Max(1000, _settings.ScriptedConversation.MessageTimeoutMs);

            for (var i = 0; i < prompts.Count; i++)
            {
                var send = await SendMessageAsync(createSession.SessionId, new RealtimeHttpSendMessageRequest
                {
                    Text = prompts[i],
                    TimeoutMs = messageTimeoutMs
                }, cancellationToken).ConfigureAwait(false);

                scriptedTurns.Add(new RealtimeHttpConversationTurnResponse
                {
                    Index = i + 1,
                    InputText = prompts[i],
                    OutputText = send.OutputText,
                    Completed = send.Completed,
                    TurnNumber = send.TurnNumber,
                    CreatedAt = send.CreatedAt
                });
            }

            var disconnect = await DisconnectSessionAsync(createSession.SessionId, closeReason, cancellationToken).ConfigureAwait(false);
            disconnected = disconnect.Closed;

            return new RealtimeHttpRunDefaultConversationResponse
            {
                SessionId = createSession.SessionId,
                ProviderSessionId = string.IsNullOrWhiteSpace(disconnect.ProviderSessionId) ? createSession.ProviderSessionId : disconnect.ProviderSessionId,
                AssistantId = createSession.AssistantId,
                Region = createSession.Region,
                WarmupTurnCompleted = false,
                WarmupTurnNumber = 0,
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
        return await _recordingInfoReader.GetRecordingInfoAsync(
            sessionIdOrProviderSessionId,
            providerSessionId,
            BuildRecordingDownloadUrl,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<RealtimeHttpSessionDetailResponse> GetSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            if (_closedSessions.TryGetValue(sessionId, out var closed))
                return Task.FromResult(closed.Detail);

            return Task.FromResult<RealtimeHttpSessionDetailResponse>(null);
        }

        return Task.FromResult(session.GetDetail());
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
            if (_closedSessions.TryGetValue(sessionId, out var closed))
            {
                return new RealtimeHttpDisconnectResponse
                {
                    SessionId = sessionId,
                    ProviderSessionId = closed.ProviderSessionId,
                    Closed = false,
                    Reason = closed.Reason
                };
            }

            return new RealtimeHttpDisconnectResponse
            {
                SessionId = sessionId,
                Closed = false,
                Reason = "session_not_found"
            };
        }

        await session.CloseAsync(reason, cancellationToken).ConfigureAwait(false);

        return new RealtimeHttpDisconnectResponse
        {
            SessionId = sessionId,
            ProviderSessionId = session.ProviderSessionId,
            Closed = true,
            Reason = reason
        };
    }

    private RealtimeHttpSession GetActiveSessionOrThrow(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            return session;

        if (_closedSessions.TryGetValue(sessionId, out var closed))
            throw RealtimeHttpGatewayException.SessionClosed(sessionId, closed.ProviderSessionId, closed.Reason);

        throw RealtimeHttpGatewayException.SessionNotFound(sessionId);
    }

    private void StoreClosedSession(RealtimeHttpSession session, string reason)
    {
        _sessions.TryRemove(session.SessionId, out _);
        var detail = session.GetDetail();
        detail.Status = "closed";
        detail.CloseReason = reason;

        _closedSessions[session.SessionId] = new ClosedSessionInfo
        {
            SessionId = session.SessionId,
            ProviderSessionId = session.ProviderSessionId,
            ClosedAt = DateTimeOffset.UtcNow,
            Reason = reason,
            Detail = detail
        };

        PruneClosedSessions();
    }

    private void PruneClosedSessions()
    {
        var retentionMs = Math.Max(1000, _settings.ClosedSessionRetentionMs);
        var cutoff = DateTimeOffset.UtcNow.AddMilliseconds(-retentionMs);
        foreach (var item in _closedSessions.Where(x => x.Value.ClosedAt < cutoff).ToList())
            _closedSessions.TryRemove(item.Key, out _);

        var capacity = Math.Max(1, _settings.ClosedSessionCapacity);
        var overflow = _closedSessions.Count - capacity;
        if (overflow <= 0) return;

        foreach (var item in _closedSessions.OrderBy(x => x.Value.ClosedAt).Take(overflow).ToList())
            _closedSessions.TryRemove(item.Key, out _);
    }

    private string ResolveProviderSessionId(string sessionIdOrProviderSessionId)
    {
        if (_sessions.TryGetValue(sessionIdOrProviderSessionId, out var activeSession)
            && !string.IsNullOrWhiteSpace(activeSession.ProviderSessionId))
        {
            return activeSession.ProviderSessionId;
        }

        if (_closedSessions.TryGetValue(sessionIdOrProviderSessionId, out var closedSession)
            && !string.IsNullOrWhiteSpace(closedSession.ProviderSessionId))
        {
            return closedSession.ProviderSessionId;
        }

        return Guid.TryParse(sessionIdOrProviderSessionId, out _) ? sessionIdOrProviderSessionId : string.Empty;
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

    private sealed class ClosedSessionInfo
    {
        public string SessionId { get; init; } = string.Empty;

        public string ProviderSessionId { get; init; } = string.Empty;

        public DateTimeOffset ClosedAt { get; init; }

        public string Reason { get; init; } = string.Empty;

        public RealtimeHttpSessionDetailResponse Detail { get; init; } = new();
    }
}
