using Shouldly;
using SmartTalk.Core.Services.Timer;
using Xunit;

namespace SmartTalk.UnitTests.Services.Timer;

public class InactivityTimerManagerTests : IDisposable
{
    private readonly InactivityTimerManager _sut = new();
    private const string SessionId = "test-session";

    public void Dispose()
    {
        _sut.StopTimer(SessionId);
        _sut.StopTimer("session-1");
        _sut.StopTimer("session-2");
    }

    #region StartTimer

    [Fact]
    public async Task StartTimer_ShouldInvokeCallback_AfterTimeout()
    {
        var tcs = new TaskCompletionSource<bool>();

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(50), () =>
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(tcs.Task, "Callback should have been invoked within timeout");
        tcs.Task.Result.ShouldBeTrue();
    }

    [Fact]
    public async Task StartTimer_ShouldNotInvokeCallback_BeforeTimeout()
    {
        var callbackInvoked = false;

        _sut.StartTimer(SessionId, TimeSpan.FromSeconds(5), () =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        await Task.Delay(100);
        callbackInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task StartTimer_ShouldCancelPreviousTimer_WhenSameSessionId()
    {
        var firstCallbackInvoked = false;
        var secondTcs = new TaskCompletionSource<bool>();

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(200), () =>
        {
            firstCallbackInvoked = true;
            return Task.CompletedTask;
        });

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(50), () =>
        {
            secondTcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        await Task.WhenAny(secondTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Task.Delay(300); // Wait long enough for first timer to have fired if not cancelled

        secondTcs.Task.IsCompleted.ShouldBeTrue();
        firstCallbackInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task StartTimer_MultipleSessions_ShouldWorkIndependently()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();

        _sut.StartTimer("session-1", TimeSpan.FromMilliseconds(50), () =>
        {
            tcs1.TrySetResult(true);
            return Task.CompletedTask;
        });

        _sut.StartTimer("session-2", TimeSpan.FromMilliseconds(50), () =>
        {
            tcs2.TrySetResult(true);
            return Task.CompletedTask;
        });

        _sut.StopTimer("session-1");

        await Task.WhenAny(tcs2.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Task.Delay(150);

        tcs1.Task.IsCompleted.ShouldBeFalse("Stopped session should not fire");
        tcs2.Task.IsCompleted.ShouldBeTrue("Other session should fire independently");
    }

    [Fact]
    public async Task StartTimer_PreviousTimerFinally_ShouldNotRemoveNewTimer()
    {
        var firstCallbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCallbackInvoked = false;

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(20), async () =>
        {
            firstCallbackStarted.TrySetResult(true);
            await releaseFirstCallback.Task.ConfigureAwait(false);
        });

        var firstStarted = await Task.WhenAny(firstCallbackStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        firstStarted.ShouldBe(firstCallbackStarted.Task, "First callback should start");

        _sut.StartTimer(SessionId, TimeSpan.FromSeconds(5), () =>
        {
            secondCallbackInvoked = true;
            return Task.CompletedTask;
        });

        releaseFirstCallback.TrySetResult(true);
        await Task.Delay(100); // Allow previous timer finally block to execute

        _sut.IsTimerRunning(SessionId).ShouldBeTrue("New timer should remain registered");

        _sut.StopTimer(SessionId);
        await Task.Delay(100);

        secondCallbackInvoked.ShouldBeFalse("Stopped replacement timer should not fire");
    }

    [Fact]
    public async Task StartTimer_RapidSequentialStartStopCycles_NoSpuriousCallback()
    {
        // Real-world production pattern: every AI turn completion calls StartTimer; every
        // user-speech-detected event calls StopTimer; conversation oscillates rapidly.
        //
        // Background (the bug this fix protects against): each StopTimer cancels the entry's
        // CTS, the cancelled RunTimerAsync hits its finally block which (pre-fix) did
        // unconditional `_timers.TryRemove(sessionId)`. If a new StartTimer raced in between
        // the StopTimer and the cancelled finally, the finally clobbered the *new* timer's
        // dict registration. The new timer's Task.Delay then ran with a never-cancelled CTS,
        // IsTimerRunning returned false (so subsequent StopTimer calls became no-ops for it),
        // and after timeout the callback fired — in production scheduling a Hangfire
        // HangupCallAsync job and disconnecting the user mid-conversation.
        //
        // Post-fix: the finally uses ICollection.Remove with key+value match, so an old
        // cancelled timer's finally cannot clobber a new timer's registration.
        //
        // What THIS test pins: the broader invariant — under rapid Start/Stop pairing
        // (the production AI-turn / user-speech alternation), NO callback should ever
        // fire and the dict must end up empty. It catches a class of regressions:
        // StopTimer failing to cancel its CTS, StartTimer failing to replace the old
        // entry, leaked entries from any future race. It does NOT, on its own,
        // deterministically reproduce the specific finally-vs-StartTimer race window
        // — tight synchronous loops let each cancelled finally complete before the
        // next iteration adds a fresh entry, so the racy clobber rarely lands.
        //
        // The deterministic regression guard for the specific race is
        // `StartTimer_PreviousTimerFinally_ShouldNotRemoveNewTimer` above, which uses
        // TaskCompletionSource-controlled callback blocking to interleave a second
        // StartTimer with a still-suspended finally and asserts the new entry survives.

        var callbackCount = 0;

        for (var i = 0; i < 50; i++)
        {
            _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(20), () =>
            {
                Interlocked.Increment(ref callbackCount);
                return Task.CompletedTask;
            });
            _sut.StopTimer(SessionId);
        }

        // Wait far longer than the 20ms timeout. Any leaked / un-cancelled entry would
        // have fired its callback by now. With the fix this stays at zero.
        await Task.Delay(500);

        callbackCount.ShouldBe(0, "No callback should fire when every StartTimer is followed by StopTimer, no matter how rapid the cycles");
        _sut.IsTimerRunning(SessionId).ShouldBeFalse("Dict must be empty after the final StopTimer");
    }

    [Fact]
    public async Task StartTimer_CallbackException_ShouldNotCrashAndShouldBeReusable()
    {
        var callbackReached = new TaskCompletionSource<bool>();

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(50), () =>
        {
            callbackReached.TrySetResult(true);
            throw new InvalidOperationException("Test exception");
        });

        await Task.WhenAny(callbackReached.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        callbackReached.Task.IsCompleted.ShouldBeTrue();

        await Task.Delay(100);

        // Should be able to start a new timer without issues
        var secondTcs = new TaskCompletionSource<bool>();
        Should.NotThrow(() => _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(50), () =>
        {
            secondTcs.TrySetResult(true);
            return Task.CompletedTask;
        }));

        var completed = await Task.WhenAny(secondTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(secondTcs.Task, "New timer should work after previous callback threw");
    }

    #endregion

    #region StopTimer

    [Fact]
    public async Task StopTimer_ShouldPreventCallback()
    {
        var callbackInvoked = false;

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(100), () =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        _sut.StopTimer(SessionId);
        await Task.Delay(300);

        callbackInvoked.ShouldBeFalse();
    }

    [Fact]
    public void StopTimer_ShouldNotThrow_WhenSessionNotExists()
    {
        Should.NotThrow(() => _sut.StopTimer("nonexistent"));
    }

    [Fact]
    public void StopTimer_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        _sut.StartTimer(SessionId, TimeSpan.FromSeconds(10), () => Task.CompletedTask);

        Should.NotThrow(() =>
        {
            _sut.StopTimer(SessionId);
            _sut.StopTimer(SessionId);
        });
    }

    #endregion

    #region ResetTimer

    [Fact]
    public async Task ResetTimer_ShouldRestartWithSameCallbackAndTimeout()
    {
        var callCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(500), () =>
        {
            Interlocked.Increment(ref callCount);
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        await Task.Delay(50); // Brief wait, well before 500ms
        _sut.ResetTimer(SessionId); // Should restart the 500ms countdown

        // Should not have fired yet (only ~50ms into reset)
        callCount.ShouldBe(0, "Callback should not fire before reset timeout");

        // Should fire after the full reset timeout
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(tcs.Task, "Callback should fire after reset timeout");
        callCount.ShouldBe(1);
    }

    [Fact]
    public void ResetTimer_ShouldNotThrow_WhenSessionNotExists()
    {
        Should.NotThrow(() => _sut.ResetTimer("nonexistent"));
    }

    [Fact]
    public void ResetTimer_ShouldKeepTimerRunning()
    {
        _sut.StartTimer(SessionId, TimeSpan.FromSeconds(5), () => Task.CompletedTask);

        _sut.ResetTimer(SessionId);

        _sut.IsTimerRunning(SessionId).ShouldBeTrue();
    }

    #endregion

    #region IsTimerRunning

    [Fact]
    public void IsTimerRunning_ShouldReturnTrue_WhenTimerActive()
    {
        _sut.StartTimer(SessionId, TimeSpan.FromSeconds(10), () => Task.CompletedTask);

        _sut.IsTimerRunning(SessionId).ShouldBeTrue();
    }

    [Fact]
    public void IsTimerRunning_ShouldReturnFalse_WhenTimerStopped()
    {
        _sut.StartTimer(SessionId, TimeSpan.FromSeconds(10), () => Task.CompletedTask);
        _sut.StopTimer(SessionId);

        _sut.IsTimerRunning(SessionId).ShouldBeFalse();
    }

    [Fact]
    public void IsTimerRunning_ShouldReturnFalse_WhenSessionNotExists()
    {
        _sut.IsTimerRunning("nonexistent").ShouldBeFalse();
    }

    [Fact]
    public async Task IsTimerRunning_ShouldReturnFalse_AfterCallbackCompletes()
    {
        var tcs = new TaskCompletionSource<bool>();

        _sut.StartTimer(SessionId, TimeSpan.FromMilliseconds(50), () =>
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Task.Delay(100); // Allow cleanup

        _sut.IsTimerRunning(SessionId).ShouldBeFalse("Timer entry should be removed after callback completes");
    }

    #endregion
}
