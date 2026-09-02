using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Core.Settings.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Caching;
using Xunit;

namespace SmartTalk.UnitTests.Services.InterviewSession;

public class AiSpeechAssistantSessionCredentialServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public async Task StoreAsync_UsesConfiguredCredentialLifetime()
    {
        var fixture = CreateFixture(sessionCredentialLifetimeMinutes: 90);
        var session = CreateSession(Now);

        await fixture.Service.StoreAsync(session);

        await fixture.CacheManager.Received(1).SetAsync(
            AiSpeechAssistantSessionCredentialCacheKeys.GetCacheKey(session.SessionId),
            Arg.Is<AiSpeechAssistantSessionCredential>(x =>
                x.SessionId == session.SessionId &&
                x.AssistantId == session.AssistantId &&
                x.ExpiresAt == Now.AddMinutes(90)),
            Arg.Is<ICachingSetting>(x => x.Expiry == TimeSpan.FromMinutes(90)),
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
            AiSpeechAssistantSessionCredentialCacheKeys.GetCacheKey(session.SessionId),
            Arg.Any<AiSpeechAssistantSessionCredential>(),
            Arg.Is<ICachingSetting>(x => x.Expiry == TimeSpan.FromHours(23.5)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetValidAsync_WhenCreatedTwentyFourHoursAgo_ReturnsInvalid()
    {
        var fixture = CreateFixture();
        var session = CreateSession(Now.AddHours(-24));
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
            AiSpeechAssistantSessionCredentialCacheKeys.GetCacheKey(sessionId),
            Arg.Any<ICachingSetting>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReserveWebRtcAsync_ConcurrentRequestsOnlyOneSucceeds()
    {
        var fixture = CreateFixture();
        var credential = CreateCredential(Now.AddMinutes(30));
        fixture.CacheManager.GetAsync<AiSpeechAssistantSessionCredential>(
                Arg.Any<string>(),
                Arg.Any<ICachingSetting>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => credential);

        var results = await Task.WhenAll(
            fixture.Service.ReserveWebRtcAsync(credential.SessionId, "reservation-a"),
            fixture.Service.ReserveWebRtcAsync(credential.SessionId, "reservation-b"));

        Assert.Contains(AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded, results);
        Assert.Contains(AiSpeechAssistantSessionWebRtcTransitionStatus.Conflict, results);
        Assert.Equal(AiSpeechAssistantSessionWebRtcStatus.Creating, credential.WebRtcStatus);
        Assert.Contains(credential.WebRtcReservationId, new[] { "reservation-a", "reservation-b" });
    }

    [Fact]
    public async Task ActivateWebRtcAsync_BindsCallOwnedByReservation()
    {
        var fixture = CreateFixture();
        var credential = CreateCredential(Now.AddMinutes(30));
        credential.WebRtcStatus = AiSpeechAssistantSessionWebRtcStatus.Creating;
        credential.WebRtcReservationId = "reservation-a";
        fixture.CacheManager.GetAsync<AiSpeechAssistantSessionCredential>(
                Arg.Any<string>(),
                Arg.Any<ICachingSetting>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => credential);

        var result = await fixture.Service.ActivateWebRtcAsync(
            credential.SessionId,
            "reservation-a",
            "rtc_test_123");

        Assert.Equal(AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded, result);
        Assert.Equal(AiSpeechAssistantSessionWebRtcStatus.Active, credential.WebRtcStatus);
        Assert.Equal("rtc_test_123", credential.WebRtcCallId);
        Assert.Null(credential.WebRtcReservationId);
        Assert.True(await fixture.Service.IsWebRtcCallBoundAsync(
            credential.SessionId,
            "rtc_test_123"));
        Assert.False(await fixture.Service.IsWebRtcCallBoundAsync(
            credential.SessionId,
            "rtc_other"));
    }

    [Fact]
    public async Task ReleaseWebRtcReservationAsync_DoesNotReleaseAnotherReservation()
    {
        var fixture = CreateFixture();
        var credential = CreateCredential(Now.AddMinutes(30));
        credential.WebRtcStatus = AiSpeechAssistantSessionWebRtcStatus.Creating;
        credential.WebRtcReservationId = "reservation-a";
        fixture.CacheManager.GetAsync<AiSpeechAssistantSessionCredential>(
                Arg.Any<string>(),
                Arg.Any<ICachingSetting>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => credential);

        await fixture.Service.ReleaseWebRtcReservationAsync(
            credential.SessionId,
            "reservation-b");

        Assert.Equal(AiSpeechAssistantSessionWebRtcStatus.Creating, credential.WebRtcStatus);
        Assert.Equal("reservation-a", credential.WebRtcReservationId);
        await fixture.CacheManager.DidNotReceiveWithAnyArgs()
            .SetAsync(default, default, default, default);
    }

    private static Fixture CreateFixture(int sessionCredentialLifetimeMinutes = 24 * 60)
    {
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var cacheManager = Substitute.For<ICacheManager>();
        var dataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        var redisSafeRunner = Substitute.For<IRedisSafeRunner>();
        var redisLock = new SemaphoreSlim(1, 1);
        redisSafeRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<RedisServer>())
            .Returns(async callInfo =>
            {
                await redisLock.WaitAsync();
                try
                {
                    await callInfo.ArgAt<Func<Task>>(1)();
                }
                finally
                {
                    redisLock.Release();
                }
            });

        return new Fixture(
            new AiSpeechAssistantSessionCredentialService(
                clock,
                cacheManager,
                dataProvider,
                redisSafeRunner,
                new AiSpeechAssistantSettings(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AiSpeechAssistant:SessionCredentialLifetimeMinutes"] =
                            sessionCredentialLifetimeMinutes.ToString()
                    })
                    .Build())),
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
