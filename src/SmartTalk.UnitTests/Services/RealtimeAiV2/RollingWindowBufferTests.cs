using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the contract of <see cref="RollingWindowBuffer"/>: a fixed-capacity
/// PCM buffer that drops the oldest bytes when the cap is reached.
///
/// <para>Sizing rationale (default 300 seconds × 48,000 byte/s = 14.4 MB)
/// is deliberately well above any realistic restaurant-call duration so a
/// session that completes inside the window behaves identically to the
/// unbounded buffer. The cap only kicks in for pathologically long calls
/// (30+ min, multi-restaurant) — the exact scenarios that today produce
/// 86 MB+ memory pressure per call.</para>
/// </summary>
public class RollingWindowBufferTests
{
    [Fact]
    public void Ctor_NonPositiveCapacity_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RollingWindowBuffer(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new RollingWindowBuffer(-1));
    }

    [Fact]
    public async Task SnapshotAsync_EmptyBuffer_ReturnsEmpty()
    {
        await using var buffer = new RollingWindowBuffer(10);

        (await buffer.SnapshotAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_LessThanCapacity_AppendsLinearly()
    {
        await using var buffer = new RollingWindowBuffer(10);

        await buffer.WriteAsync(new byte[] { 1, 2, 3 });
        await buffer.WriteAsync(new byte[] { 4, 5 });

        var snapshot = await buffer.SnapshotAsync();

        snapshot.ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task WriteAsync_ExactlyCapacity_FillsBuffer()
    {
        await using var buffer = new RollingWindowBuffer(5);

        await buffer.WriteAsync(new byte[] { 1, 2, 3, 4, 5 });

        var snapshot = await buffer.SnapshotAsync();

        snapshot.ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task WriteAsync_OverflowMultipleSmallWrites_DropsOldestPreservesNewest()
    {
        await using var buffer = new RollingWindowBuffer(5);

        await buffer.WriteAsync(new byte[] { 1, 2, 3, 4 });   // buffer: [1,2,3,4,_]
        await buffer.WriteAsync(new byte[] { 5, 6, 7 });      // overflow: 1,2 dropped

        var snapshot = await buffer.SnapshotAsync();

        snapshot.ShouldBe(new byte[] { 3, 4, 5, 6, 7 });
    }

    [Fact]
    public async Task WriteAsync_SingleWriteLargerThanCapacity_KeepsTrailingCapacityBytes()
    {
        await using var buffer = new RollingWindowBuffer(5);

        await buffer.WriteAsync(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var snapshot = await buffer.SnapshotAsync();

        snapshot.ShouldBe(new byte[] { 4, 5, 6, 7, 8 });
    }

    [Fact]
    public async Task WriteAsync_AfterWrap_PreservesChronologicalOrder()
    {
        await using var buffer = new RollingWindowBuffer(4);

        await buffer.WriteAsync(new byte[] { 1, 2, 3, 4 });   // exactly fills
        await buffer.WriteAsync(new byte[] { 5 });             // wraps: drops 1
        await buffer.WriteAsync(new byte[] { 6, 7 });          // drops 2,3

        var snapshot = await buffer.SnapshotAsync();

        snapshot.ShouldBe(new byte[] { 4, 5, 6, 7 });
    }

    [Fact]
    public async Task SnapshotAsync_NonDestructive_AllowsContinuedWrites()
    {
        await using var buffer = new RollingWindowBuffer(10);
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
        await using var buffer = new RollingWindowBuffer(10);
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });

        var extracted = await buffer.ExtractAsync();
        var afterExtract = await buffer.SnapshotAsync();

        extracted.ShouldBe(new byte[] { 1, 2, 3 });
        afterExtract.ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_AfterExtract_IsNoOp()
    {
        await using var buffer = new RollingWindowBuffer(10);
        await buffer.WriteAsync(new byte[] { 1 });
        await buffer.ExtractAsync();

        await buffer.WriteAsync(new byte[] { 99 });

        (await buffer.SnapshotAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_AllowsRepeatedCalls()
    {
        var buffer = new RollingWindowBuffer(10);
        await buffer.WriteAsync(new byte[] { 1 });

        await buffer.DisposeAsync();
        await buffer.DisposeAsync();
    }

    // ── Race safety: late frame arrives after Dispose ───────────
    //
    // Same scenario as the unbounded buffer: a frame already past the null check
    // can call WriteAsync on the captured reference after cleanup ran. The
    // original V2 code never disposed its session-scoped SemaphoreSlim, so the
    // late call gracefully no-ops. These tests pin the same contract.

    [Fact]
    public async Task WriteAsync_AfterDispose_NoOpsInsteadOfThrowing()
    {
        var buffer = new RollingWindowBuffer(10);
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });
        await buffer.DisposeAsync();

        var ex = await Record.ExceptionAsync(() => buffer.WriteAsync(new byte[] { 99 }));

        ex.ShouldBeNull();
    }

    [Fact]
    public async Task SnapshotAsync_AfterDispose_ReturnsEmptyInsteadOfThrowing()
    {
        var buffer = new RollingWindowBuffer(10);
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
        var buffer = new RollingWindowBuffer(10);
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
        // Stress test: 16 concurrent writers vs disposer. Whatever the interleaving,
        // no method must throw — the cap simply enforces "at most capacity" bytes.
        const int writers = 16;
        const int capacity = 4096;
        var buffer = new RollingWindowBuffer(capacity);

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
    public async Task ConcurrentWriteAndSnapshot_PreservesAtMostCapacityBytes()
    {
        const int capacity = 4096;
        await using var buffer = new RollingWindowBuffer(capacity);

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

        var snapshotTasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(async () => await buffer.SnapshotAsync()))
            .ToArray();

        await Task.WhenAll(writeTasks.Concat(snapshotTasks));

        var final = await buffer.SnapshotAsync();
        final.Length.ShouldBeLessThanOrEqualTo(capacity);
        // Total written = 8 * 1024 = 8192 bytes; capacity = 4096 → buffer should be exactly full
        final.Length.ShouldBe(capacity);
    }
}
