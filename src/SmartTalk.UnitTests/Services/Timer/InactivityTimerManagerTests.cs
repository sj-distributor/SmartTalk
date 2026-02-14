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
