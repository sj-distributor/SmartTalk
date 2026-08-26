using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Messages.Enums.Caching;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public static class AiSpeechAssistantSessionCredentialDefaults
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public static string GetCacheKey(Guid sessionId) => $"ai-speech-assistant-session:{sessionId:D}";

    public static string GetWebRtcLockKey(Guid sessionId) => $"ai-speech-assistant-session-webrtc:{sessionId:D}";
}

public enum AiSpeechAssistantSessionWebRtcStatus
{
    Available,
    Creating,
    Active
}

public enum AiSpeechAssistantSessionWebRtcTransitionStatus
{
    Succeeded,
    Conflict,
    Unavailable
}

public sealed class AiSpeechAssistantSessionCredential
{
    public Guid SessionId { get; set; }

    public int AssistantId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public AiSpeechAssistantSessionWebRtcStatus WebRtcStatus { get; set; }

    public string WebRtcCallId { get; set; }

    public string WebRtcReservationId { get; set; }
}

public interface IAiSpeechAssistantSessionCredentialService : IScopedDependency
{
    Task StoreAsync(AiSpeechAssistantSession session, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantSessionCredential> GetValidAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantSessionWebRtcTransitionStatus> ReserveWebRtcAsync(
        Guid sessionId,
        string reservationId,
        CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantSessionWebRtcTransitionStatus> ActivateWebRtcAsync(
        Guid sessionId,
        string reservationId,
        string callId,
        CancellationToken cancellationToken = default);

    Task ReleaseWebRtcReservationAsync(
        Guid sessionId,
        string reservationId,
        CancellationToken cancellationToken = default);

    Task<bool> IsWebRtcCallBoundAsync(
        Guid sessionId,
        string callId,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed class AiSpeechAssistantSessionCredentialService : IAiSpeechAssistantSessionCredentialService
{
    private readonly IClock _clock;
    private readonly ICacheManager _cacheManager;
    private readonly IAiSpeechAssistantDataProvider _dataProvider;
    private readonly IRedisSafeRunner _redisSafeRunner;

    public AiSpeechAssistantSessionCredentialService(
        IClock clock,
        ICacheManager cacheManager,
        IAiSpeechAssistantDataProvider dataProvider,
        IRedisSafeRunner redisSafeRunner)
    {
        _clock = clock;
        _cacheManager = cacheManager;
        _dataProvider = dataProvider;
        _redisSafeRunner = redisSafeRunner;
    }

    public async Task StoreAsync(AiSpeechAssistantSession session, CancellationToken cancellationToken = default)
    {
        var credential = CreateCredential(session);
        var remainingLifetime = credential.ExpiresAt - _clock.Now;
        if (remainingLifetime <= TimeSpan.Zero) return;

        await _cacheManager.SetAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(session.SessionId),
            credential,
            new RedisCachingSetting(expiry: remainingLifetime),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantSessionCredential> GetValidAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty) return null;

        var cacheKey = AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(sessionId);
        var credential = await _cacheManager.GetAsync<AiSpeechAssistantSessionCredential>(
            cacheKey,
            new RedisCachingSetting(),
            cancellationToken).ConfigureAwait(false);

        if (IsValid(credential, sessionId)) return credential;

        var session = await _dataProvider
            .GetAiSpeechAssistantSessionBySessionIdAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session == null || session.Count > 0) return null;

        credential = CreateCredential(session);
        if (!IsValid(credential, sessionId)) return null;

        await _cacheManager.SetAsync(
            cacheKey,
            credential,
            new RedisCachingSetting(expiry: credential.ExpiresAt - _clock.Now),
            cancellationToken).ConfigureAwait(false);

        return credential;
    }

    public async Task<AiSpeechAssistantSessionWebRtcTransitionStatus> ReserveWebRtcAsync(
        Guid sessionId,
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(reservationId))
            return AiSpeechAssistantSessionWebRtcTransitionStatus.Unavailable;

        return await UpdateWebRtcCredentialAsync(
            sessionId,
            credential =>
            {
                if (credential.WebRtcStatus != AiSpeechAssistantSessionWebRtcStatus.Available)
                    return AiSpeechAssistantSessionWebRtcTransitionStatus.Conflict;

                credential.WebRtcStatus = AiSpeechAssistantSessionWebRtcStatus.Creating;
                credential.WebRtcReservationId = reservationId;
                credential.WebRtcCallId = null;
                return AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantSessionWebRtcTransitionStatus> ActivateWebRtcAsync(
        Guid sessionId,
        string reservationId,
        string callId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(reservationId) || string.IsNullOrWhiteSpace(callId))
            return AiSpeechAssistantSessionWebRtcTransitionStatus.Unavailable;

        return await UpdateWebRtcCredentialAsync(
            sessionId,
            credential =>
            {
                if (credential.WebRtcStatus != AiSpeechAssistantSessionWebRtcStatus.Creating ||
                    !string.Equals(credential.WebRtcReservationId, reservationId, StringComparison.Ordinal))
                    return AiSpeechAssistantSessionWebRtcTransitionStatus.Conflict;

                credential.WebRtcStatus = AiSpeechAssistantSessionWebRtcStatus.Active;
                credential.WebRtcReservationId = null;
                credential.WebRtcCallId = callId;
                return AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseWebRtcReservationAsync(
        Guid sessionId,
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(reservationId)) return;

        await UpdateWebRtcCredentialAsync(
            sessionId,
            credential =>
            {
                if (credential.WebRtcStatus != AiSpeechAssistantSessionWebRtcStatus.Creating ||
                    !string.Equals(credential.WebRtcReservationId, reservationId, StringComparison.Ordinal))
                    return AiSpeechAssistantSessionWebRtcTransitionStatus.Conflict;

                credential.WebRtcStatus = AiSpeechAssistantSessionWebRtcStatus.Available;
                credential.WebRtcReservationId = null;
                credential.WebRtcCallId = null;
                return AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsWebRtcCallBoundAsync(
        Guid sessionId,
        string callId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(callId)) return false;

        var credential = await GetCachedCredentialAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return IsValid(credential, sessionId) &&
               credential.WebRtcStatus == AiSpeechAssistantSessionWebRtcStatus.Active &&
               string.Equals(credential.WebRtcCallId, callId, StringComparison.Ordinal);
    }

    public Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _cacheManager.RemoveAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(sessionId),
            new RedisCachingSetting(),
            cancellationToken);
    }

    private Task<AiSpeechAssistantSessionCredential> GetCachedCredentialAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return _cacheManager.GetAsync<AiSpeechAssistantSessionCredential>(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(sessionId),
            new RedisCachingSetting(),
            cancellationToken);
    }

    private async Task<bool> StoreCredentialAsync(
        AiSpeechAssistantSessionCredential credential,
        CancellationToken cancellationToken)
    {
        var remainingLifetime = credential.ExpiresAt - _clock.Now;
        if (remainingLifetime <= TimeSpan.Zero) return false;

        await _cacheManager.SetAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(credential.SessionId),
            credential,
            new RedisCachingSetting(expiry: remainingLifetime),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<AiSpeechAssistantSessionWebRtcTransitionStatus> UpdateWebRtcCredentialAsync(
        Guid sessionId,
        Func<AiSpeechAssistantSessionCredential, AiSpeechAssistantSessionWebRtcTransitionStatus> update,
        CancellationToken cancellationToken)
    {
        var result = AiSpeechAssistantSessionWebRtcTransitionStatus.Unavailable;
        await _redisSafeRunner.ExecuteWithLockAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetWebRtcLockKey(sessionId),
            async () =>
            {
                var credential = await GetCachedCredentialAsync(sessionId, cancellationToken).ConfigureAwait(false);
                if (!IsValid(credential, sessionId)) return;

                result = update(credential);
                if (result == AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded &&
                    !await StoreCredentialAsync(credential, cancellationToken).ConfigureAwait(false))
                    result = AiSpeechAssistantSessionWebRtcTransitionStatus.Unavailable;
            },
            wait: TimeSpan.FromSeconds(3),
            retry: TimeSpan.FromMilliseconds(100),
            server: RedisServer.System).ConfigureAwait(false);

        return result;
    }

    private bool IsValid(AiSpeechAssistantSessionCredential credential, Guid sessionId)
    {
        return credential != null &&
               credential.SessionId == sessionId &&
               _clock.Now < credential.ExpiresAt;
    }

    private static AiSpeechAssistantSessionCredential CreateCredential(AiSpeechAssistantSession session)
    {
        return new AiSpeechAssistantSessionCredential
        {
            SessionId = session.SessionId,
            AssistantId = session.AssistantId,
            ExpiresAt = session.CreatedDate.Add(AiSpeechAssistantSessionCredentialDefaults.Lifetime)
        };
    }
}
