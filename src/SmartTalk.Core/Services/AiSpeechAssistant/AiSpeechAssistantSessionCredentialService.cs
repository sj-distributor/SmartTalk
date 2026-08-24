using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Infrastructure;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public static class AiSpeechAssistantSessionCredentialDefaults
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public static string GetCacheKey(Guid sessionId) => $"ai-speech-assistant-session:{sessionId:D}";
}

public sealed class AiSpeechAssistantSessionCredential
{
    public Guid SessionId { get; set; }

    public int AssistantId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

public interface IAiSpeechAssistantSessionCredentialService : IScopedDependency
{
    Task StoreAsync(AiSpeechAssistantSession session, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantSessionCredential> GetValidAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed class AiSpeechAssistantSessionCredentialService : IAiSpeechAssistantSessionCredentialService
{
    private readonly IClock _clock;
    private readonly ICacheManager _cacheManager;
    private readonly IAiSpeechAssistantDataProvider _dataProvider;

    public AiSpeechAssistantSessionCredentialService(
        IClock clock,
        ICacheManager cacheManager,
        IAiSpeechAssistantDataProvider dataProvider)
    {
        _clock = clock;
        _cacheManager = cacheManager;
        _dataProvider = dataProvider;
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

    public Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _cacheManager.RemoveAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(sessionId),
            new RedisCachingSetting(),
            cancellationToken);
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
