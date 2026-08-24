using NSubstitute;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Infrastructure;
using Xunit;

namespace SmartTalk.UnitTests.Services.InterviewSession;

public class AiSpeechAssistantSessionCredentialServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public async Task StoreAsync_SetsCredentialWithOneHourLifetime()
    {
        var fixture = CreateFixture();
        var session = CreateSession(Now);

        await fixture.Service.StoreAsync(session);

        await fixture.CacheManager.Received(1).SetAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(session.SessionId),
            Arg.Is<AiSpeechAssistantSessionCredential>(x =>
                x.SessionId == session.SessionId &&
                x.AssistantId == session.AssistantId &&
                x.ExpiresAt == Now.AddHours(1)),
            Arg.Is<ICachingSetting>(x => x.Expiry == TimeSpan.FromHours(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetValidAsync_ReturnsValidCachedCredentialWithoutQueryingDatabase()
    {
        var fixture = CreateFixture();
        var credential = CreateCredential(Now.AddMinutes(1));
        fixture.CacheManager.GetAsync<AiSpeechAssistantSessionCredential>(
                Arg.Any<string>(),
                Arg.Any<ICachingSetting>(),
                Arg.Any<CancellationToken>())
            .Returns(credential);

        var result = await fixture.Service.GetValidAsync(credential.SessionId);

        Assert.Same(credential, result);
        await fixture.DataProvider.DidNotReceiveWithAnyArgs()
            .GetAiSpeechAssistantSessionBySessionIdAsync(default, default);
    }

    [Fact]
    public async Task GetValidAsync_WhenCacheIsMissing_RehydratesUsingDatabaseRemainingLifetime()
    {
        var fixture = CreateFixture();
        var session = CreateSession(Now.AddMinutes(-30));
        fixture.DataProvider.GetAiSpeechAssistantSessionBySessionIdAsync(
                session.SessionId,
                Arg.Any<CancellationToken>())
            .Returns(session);

        var result = await fixture.Service.GetValidAsync(session.SessionId);

        Assert.NotNull(result);
        Assert.Equal(session.AssistantId, result.AssistantId);
        await fixture.CacheManager.Received(1).SetAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(session.SessionId),
            Arg.Any<AiSpeechAssistantSessionCredential>(),
            Arg.Is<ICachingSetting>(x => x.Expiry == TimeSpan.FromMinutes(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetValidAsync_WhenCreatedOneHourAgo_ReturnsInvalid()
    {
        var fixture = CreateFixture();
        var session = CreateSession(Now.AddHours(-1));
        fixture.DataProvider.GetAiSpeechAssistantSessionBySessionIdAsync(
                session.SessionId,
                Arg.Any<CancellationToken>())
            .Returns(session);

        var result = await fixture.Service.GetValidAsync(session.SessionId);

        Assert.Null(result);
        await fixture.CacheManager.DidNotReceiveWithAnyArgs()
            .SetAsync(default, default, default, default);
    }

    [Fact]
    public async Task GetValidAsync_WhenCacheIsMissingAndSessionWasUsed_DoesNotRehydrate()
    {
        var fixture = CreateFixture();
        var session = CreateSession(Now.AddMinutes(-30));
        session.Count = 1;
        fixture.DataProvider.GetAiSpeechAssistantSessionBySessionIdAsync(
                session.SessionId,
                Arg.Any<CancellationToken>())
            .Returns(session);

        var result = await fixture.Service.GetValidAsync(session.SessionId);

        Assert.Null(result);
        await fixture.CacheManager.DidNotReceiveWithAnyArgs()
            .SetAsync(default, default, default, default);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCredentialFromRedis()
    {
        var fixture = CreateFixture();
        var sessionId = Guid.Parse("753a2495-e52f-498c-8cd2-900de42f90ce");

        await fixture.Service.InvalidateAsync(sessionId);

        await fixture.CacheManager.Received(1).RemoveAsync(
            AiSpeechAssistantSessionCredentialDefaults.GetCacheKey(sessionId),
            Arg.Any<ICachingSetting>(),
            Arg.Any<CancellationToken>());
    }

    private static Fixture CreateFixture()
    {
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var cacheManager = Substitute.For<ICacheManager>();
        var dataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();

        return new Fixture(
            new AiSpeechAssistantSessionCredentialService(clock, cacheManager, dataProvider),
            cacheManager,
            dataProvider);
    }

    private static AiSpeechAssistantSession CreateSession(DateTimeOffset createdDate)
    {
        return new AiSpeechAssistantSession
        {
            AssistantId = 123,
            SessionId = Guid.Parse("753a2495-e52f-498c-8cd2-900de42f90ce"),
            CreatedDate = createdDate
        };
    }

    private static AiSpeechAssistantSessionCredential CreateCredential(DateTimeOffset expiresAt)
    {
        return new AiSpeechAssistantSessionCredential
        {
            AssistantId = 123,
            SessionId = Guid.Parse("753a2495-e52f-498c-8cd2-900de42f90ce"),
            ExpiresAt = expiresAt
        };
    }

    private sealed record Fixture(
        AiSpeechAssistantSessionCredentialService Service,
        ICacheManager CacheManager,
        IAiSpeechAssistantDataProvider DataProvider);
}
