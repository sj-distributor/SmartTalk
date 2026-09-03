using System.Collections.Concurrent;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The client read loop reassembles WebSocket continuation frames into one message before parsing
/// it. That branch had no coverage: the fake always reported EndOfMessage, so every existing test
/// exercised the whole-frame case only, and a message split across frames — which is what a real
/// client does with anything past its fragment size — would have been silently truncated or
/// mis-parsed with nothing going red.
///
/// <para>Pinned before the read path is touched for allocation, since a fast path for the common
/// whole-frame case is exactly the change that can break reassembly without breaking anything else.</para>
/// </summary>
public class RealtimeAiServiceClientFrameReadTests : RealtimeAiServiceTestBase
{
    private ConcurrentQueue<string> CaptureParsedMessages()
    {
        var seen = new ConcurrentQueue<string>();

        ClientAdapter.ParseMessage(Arg.Any<string>()).Returns(call =>
        {
            seen.Enqueue(call.Arg<string>());
            return new ParsedClientMessage { Type = RealtimeAiClientMessageType.Unknown };
        });

        return seen;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException($"Timed out after 5s waiting for: {description}");
    }

    [Fact]
    public async Task AMessageSplitAcrossFrames_ShouldReachTheAdapterWhole()
    {
        var seen = CaptureParsedMessages();
        var sessionTask = await StartSessionInBackgroundAsync();

        // Deliberately not a multiple of the chunk size, so the final frame is a partial one.
        const string payload = """{"event":"media","media":{"payload":"0123456789abcdef"}}""";
        FakeWs.EnqueueFragmentedClientMessage(payload, chunkSize: 7);

        await WaitUntilAsync(() => seen.Count == 1, "the fragmented message to be parsed");

        seen.Single().ShouldBe(payload);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AWholeFrameAndAFragmentedOne_ShouldBothArriveIntactAndInOrder()
    {
        // Reassembly must not leak state between messages: a buffer reused across the two, or a
        // length not reset, shows up here as one message carrying the other's tail.
        var seen = CaptureParsedMessages();
        var sessionTask = await StartSessionInBackgroundAsync();

        const string whole = """{"event":"mark"}""";
        const string fragmented = """{"event":"media","media":{"payload":"AAAAAAAAAAAAAAAAAAAA"}}""";

        FakeWs.EnqueueClientMessage(whole);
        FakeWs.EnqueueFragmentedClientMessage(fragmented, chunkSize: 5);

        await WaitUntilAsync(() => seen.Count == 2, "both messages to be parsed");

        seen.ToArray().ShouldBe([whole, fragmented]);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AShortFragmentedMessageFollowedByALongerOne_ShouldNotCarryTheFirstsTail()
    {
        // The pooled receive buffer is reused frame to frame and never cleared. Anything that
        // decodes it by buffer length rather than by the frame's own count reads the previous
        // message's bytes as a suffix — which no existing test could see.
        var seen = CaptureParsedMessages();
        var sessionTask = await StartSessionInBackgroundAsync();

        const string longFirst = """{"event":"media","media":{"payload":"ZZZZZZZZZZZZZZZZZZZZZZZZ"}}""";
        const string shortSecond = """{"e":"x"}""";

        FakeWs.EnqueueFragmentedClientMessage(longFirst, chunkSize: 9);
        FakeWs.EnqueueClientMessage(shortSecond);

        await WaitUntilAsync(() => seen.Count == 2, "both messages to be parsed");

        seen.ToArray().ShouldBe([longFirst, shortSecond]);

        FakeWs.EnqueueClose();
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AClientClosingMidMessage_ShouldEndTheSessionAsAClose()
    {
        // A half-sent message when the caller hangs up must be read as a hangup, not as a message.
        var seen = CaptureParsedMessages();
        var sessionEnded = false;
        var options = CreateDefaultOptions(o => o.OnSessionEndedAsync = _ => { sessionEnded = true; return Task.CompletedTask; });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        FakeWs.EnqueuePartialClientMessage("""{"event":"med""");
        FakeWs.EnqueueClose();

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        seen.ShouldBeEmpty("a message that never completed must not be parsed");
        sessionEnded.ShouldBeTrue();
    }
}
