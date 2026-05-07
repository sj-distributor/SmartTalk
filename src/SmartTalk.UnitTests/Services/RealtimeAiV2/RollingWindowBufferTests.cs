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
