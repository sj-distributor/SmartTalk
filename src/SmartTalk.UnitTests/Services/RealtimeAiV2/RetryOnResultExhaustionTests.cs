using Shouldly;
using SmartTalk.Core.Utils;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// RetryOnResultAsync returns its LAST result even when the retry predicate still matches — it does
/// not throw on exhaustion. Every caller therefore has to handle the still-unsatisfactory value.
///
/// <para>The Twilio recording callback did not, and dereferenced a record that is null for every call
/// that was recorded but never got a row — which is every forwarded call, since forwarding triggers
/// the recording while the record is deliberately not created. Pinned here rather than through the
/// service, whose own retry makes a service-level test wait thirty seconds.</para>
/// </summary>
public class RetryOnResultExhaustionTests
{
    [Fact]
    public async Task WhenEveryAttemptStillMatchesTheRetryCondition_ShouldReturnTheLastResultRatherThanThrow()
    {
        var attempts = 0;

        string result = null;

        await Should.NotThrowAsync(async () => result = await RetryHelper.RetryOnResultAsync<string>(
            _ => Task.FromResult<string>(null).ContinueWith(t => { attempts++; return t.Result; }),
            shouldRetry: value => value == null,
            maxRetryCount: 3,
            delay: TimeSpan.Zero,
            CancellationToken.None));

        result.ShouldBeNull("callers must handle the unsatisfactory value — exhaustion is silent");
        attempts.ShouldBe(4, "one initial attempt plus three retries");
    }

    [Fact]
    public async Task WhenAnAttemptSucceeds_ShouldStopRetrying()
    {
        var attempts = 0;

        var result = await RetryHelper.RetryOnResultAsync(
            _ => { attempts++; return Task.FromResult(attempts == 2 ? "found" : null); },
            shouldRetry: value => value == null,
            maxRetryCount: 3,
            delay: TimeSpan.Zero,
            CancellationToken.None);

        result.ShouldBe("found");
        attempts.ShouldBe(2);
    }
}
