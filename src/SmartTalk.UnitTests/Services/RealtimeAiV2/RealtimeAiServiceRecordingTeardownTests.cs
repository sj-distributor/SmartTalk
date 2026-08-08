using NSubstitute;
using SmartTalk.Core.Services.RealtimeAiV2;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The phone path buffers every frame of a call and then encodes a WAV nobody reads: its completion
/// handler only logged the byte count. For a ten-minute call that is roughly 125 MB of large-object
/// churn at teardown — extract to a byte[], copy into a MemoryStream behind WaveFileWriter, then
/// ToArray again — produced purely to be garbage-collected.
///
/// <para>Dropping the handler is not enough on its own: the extraction and the buffer's disposal were
/// behind the same early return, so a session with recording enabled and no consumer would skip both
/// and leak the buffer's semaphore for the life of the scope. Splitting them is what makes removing
/// the handler safe.</para>
/// </summary>
public class RealtimeAiServiceRecordingTeardownTests : RealtimeAiServiceTestBase
{
    private sealed class SpyRecordingBuffer : IRecordingBuffer
    {
        public int ExtractCount;
        public int DisposeCount;

        public Task WriteAsync(ReadOnlyMemory<byte> data) => Task.CompletedTask;

        public Task<byte[]> SnapshotAsync() => Task.FromResult(new byte[128]);

        public Task<byte[]> ExtractAsync()
        {
            ExtractCount++;
            return Task.FromResult(new byte[128]);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private readonly SpyRecordingBuffer _buffer = new();

    private RealtimeSessionOptions RecordingSession(bool wireCompletionHandler)
    {
        Sut.RecordingBufferFactoryOverride = () => _buffer;

        return CreateDefaultOptions(o =>
        {
            o.EnableRecording = true;
            if (wireCompletionHandler) o.OnRecordingCompleteAsync = (_, _) => Task.CompletedTask;
        });
    }

    private async Task RunAsync(RealtimeSessionOptions options)
    {
        var sessionTask = await StartSessionInBackgroundAsync(options);
        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NoCompletionHandler_ShouldSkipExtractionEntirely()
    {
        await RunAsync(RecordingSession(wireCompletionHandler: false));

        _buffer.ExtractCount.ShouldBe(0,
            "with nobody to hand the WAV to, extracting and encoding it is pure garbage");
    }

    [Fact]
    public async Task NoCompletionHandler_ShouldStillDisposeTheBuffer()
    {
        // The half of this that is easy to get wrong: skipping the work must not skip the cleanup.
        await RunAsync(RecordingSession(wireCompletionHandler: false));

        _buffer.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task WithCompletionHandler_ShouldStillExtractAndDispose()
    {
        // AiKid uploads the finished WAV, so this path must be untouched.
        await RunAsync(RecordingSession(wireCompletionHandler: true));

        _buffer.ExtractCount.ShouldBe(1);
        _buffer.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RecordingDisabled_ShouldNeitherExtractNorDispose()
    {
        Sut.RecordingBufferFactoryOverride = () => _buffer;

        await RunAsync(CreateDefaultOptions(o => o.EnableRecording = false));

        _buffer.ExtractCount.ShouldBe(0);
        _buffer.DisposeCount.ShouldBe(0);
    }

    [Fact]
    public async Task MidCallSnapshot_ShouldStillWorkWithoutACompletionHandler()
    {
        // The buffer's real consumer on the phone path is the repeat-order snapshot, taken during the
        // call. Removing the end-of-session handler must not disturb it.
        ClientAdapter.ParseMessage(Arg.Any<string>()).Returns(new ParsedClientMessage
        {
            Type = RealtimeAiClientMessageType.Audio,
            Payload = Convert.ToBase64String(new byte[320])
        });

        byte[] snapshot = null;
        var options = RecordingSession(wireCompletionHandler: false);
        options.OnSessionReadyAsync = async actions => snapshot = await actions.GetRecordedAudioSnapshotAsync();

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized });

        var sessionTask = await StartSessionInBackgroundAsync(options);
        await FakeWssClient.SimulateMessageReceivedAsync("session.updated");

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        snapshot.ShouldNotBeNull();
        snapshot.Length.ShouldBeGreaterThan(0);
    }
}
