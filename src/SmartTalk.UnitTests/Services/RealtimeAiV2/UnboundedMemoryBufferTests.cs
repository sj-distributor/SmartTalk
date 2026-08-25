using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the contract of <see cref="UnboundedMemoryBuffer"/>: the in-memory
/// recording buffer that encapsulates the previous direct
/// <c>MemoryStream</c> + <c>SemaphoreSlim</c> usage in
/// <see cref="SmartTalk.Core.Services.RealtimeAiV2.Services.RealtimeAiSessionContext"/>.
///
/// <para>This is the "no behaviour change" implementation — it appends every
/// byte and returns the full accumulated PCM on snapshot/extract. The
/// <c>RollingWindowBuffer</c> introduced in PR 3.2 sits behind the same
/// interface but caps memory; pinning the unbounded contract first guards
/// against accidental drops or reorderings during the abstraction extraction.</para>
/// </summary>
public class UnboundedMemoryBufferTests
{
    [Fact]
    public async Task SnapshotAsync_EmptyBuffer_ReturnsEmptyArray()
    {
        await using var buffer = new UnboundedMemoryBuffer();

        var snapshot = await buffer.SnapshotAsync();

        snapshot.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_EmptyBuffer_ReturnsEmptyArray()
    {
        await using var buffer = new UnboundedMemoryBuffer();

        var extracted = await buffer.ExtractAsync();

        extracted.ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_ThenSnapshotAsync_ReturnsAllWrittenBytesInOrder()
    {
        await using var buffer = new UnboundedMemoryBuffer();

        await buffer.WriteAsync(new byte[] { 1, 2, 3 });
        await buffer.WriteAsync(new byte[] { 4, 5 });

        var snapshot = await buffer.SnapshotAsync();

        snapshot.ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task SnapshotAsync_NonDestructive_AllowsContinuedWrites()
    {
        await using var buffer = new UnboundedMemoryBuffer();
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });

        var firstSnapshot = await buffer.SnapshotAsync();

        await buffer.WriteAsync(new byte[] { 4, 5 });

        var secondSnapshot = await buffer.SnapshotAsync();

        firstSnapshot.ShouldBe(new byte[] { 1, 2, 3 });
        secondSnapshot.ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task ExtractAsync_DetachesBuffer_SubsequentSnapshotReturnsEmpty()
    {
        await using var buffer = new UnboundedMemoryBuffer();
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });

        var extracted = await buffer.ExtractAsync();
        var snapshotAfter = await buffer.SnapshotAsync();

        extracted.ShouldBe(new byte[] { 1, 2, 3 });
        snapshotAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_AfterExtract_IsNoOp()
    {
        await using var buffer = new UnboundedMemoryBuffer();
        await buffer.WriteAsync(new byte[] { 1 });
        await buffer.ExtractAsync();

        // Writes after extract are silently discarded — buffer is "spent".
        await buffer.WriteAsync(new byte[] { 99 });

        (await buffer.SnapshotAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_AllowsRepeatedCalls()
    {
        var buffer = new UnboundedMemoryBuffer();
        await buffer.WriteAsync(new byte[] { 1 });

        await buffer.DisposeAsync();
        await buffer.DisposeAsync();  // second dispose must not throw
    }

    // ── Race safety: late frame arrives after Dispose ───────────
    //
    // The session orchestrator nulls _ctx.AudioBuffer before disposing, so most
    // late writes never reach the buffer. But a frame already past the null check
    // can still call WriteAsync on the captured reference. The pre-refactor code
    // never disposed its session-scoped SemaphoreSlim, so the late call would
    // gracefully no-op. These tests pin the same contract for the new buffer:
    // post-dispose Write/Snapshot/Extract must NOT throw ObjectDisposedException.

    [Fact]
    public async Task WriteAsync_AfterDispose_NoOpsInsteadOfThrowing()
    {
        var buffer = new UnboundedMemoryBuffer();
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });
        await buffer.DisposeAsync();

        var ex = await Record.ExceptionAsync(() => buffer.WriteAsync(new byte[] { 99 }));

        ex.ShouldBeNull();
    }

    [Fact]
    public async Task SnapshotAsync_AfterDispose_ReturnsEmptyInsteadOfThrowing()
    {
        var buffer = new UnboundedMemoryBuffer();
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });
        await buffer.DisposeAsync();

        byte[] snapshot = null!;
        var ex = await Record.ExceptionAsync(async () => snapshot = await buffer.SnapshotAsync());

        ex.ShouldBeNull();
        snapshot.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_AfterDispose_ReturnsEmptyInsteadOfThrowing()
    {
        var buffer = new UnboundedMemoryBuffer();
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });
        await buffer.DisposeAsync();

        byte[] extracted = null!;
        var ex = await Record.ExceptionAsync(async () => extracted = await buffer.ExtractAsync());

        ex.ShouldBeNull();
        extracted.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentDisposeAndWrite_NeverThrows()
    {
        // Stress test: spawn N writers and a disposer racing simultaneously.
        // Whichever order they interleave, no call must throw.
        const int writers = 16;
        var buffer = new UnboundedMemoryBuffer();

        var startGate = new ManualResetEventSlim(false);

        var writeTasks = Enumerable.Range(0, writers)
            .Select(_ => Task.Run(async () =>
            {
                startGate.Wait();
                for (var i = 0; i < 10; i++)
                {
                    await buffer.WriteAsync(new byte[] { (byte)i });
                    await Task.Yield();
                }
            }))
            .ToArray();

        var disposeTask = Task.Run(async () =>
        {
            startGate.Wait();
            await Task.Yield();
            await buffer.DisposeAsync();
        });

        startGate.Set();

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(writeTasks.Concat(new[] { disposeTask })));

        ex.ShouldBeNull();
    }

    [Fact]
    public async Task ConcurrentWriteAndSnapshot_NoExceptionAndAllBytesObserved()
    {
        await using var buffer = new UnboundedMemoryBuffer();

        // Spawn 8 writers each appending 1024 unique bytes.
        const int writers = 8;
        const int bytesPerWriter = 1024;

        var writeTasks = Enumerable.Range(0, writers)
            .Select(w => Task.Run(async () =>
            {
                var chunk = new byte[bytesPerWriter];
                Array.Fill(chunk, (byte)w);
                await buffer.WriteAsync(chunk);
            }))
            .ToArray();

        // While they write, take 4 snapshots — must never throw or partially observe a write.
        var snapshotTasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(async () => await buffer.SnapshotAsync()))
            .ToArray();

        await Task.WhenAll(writeTasks.Concat(snapshotTasks));

        var final = await buffer.SnapshotAsync();
        final.Length.ShouldBe(writers * bytesPerWriter);
    }
}
