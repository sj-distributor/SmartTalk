using NSubstitute;
using Shouldly;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceFunctionCallTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task FunctionCall_SingleCall_ReplySentAndResponseCreateTriggered()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData>
                {
                    new() { FunctionName = "get_weather", ArgumentsJson = "{\"city\":\"NYC\"}" }
                }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = fc =>
                Task.FromResult(new RealtimeAiFunctionCallResult { ReplyMessage = "Weather is sunny" });
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Reply should be sent as text + response.create
        ProviderAdapter.Received().BuildTextUserMessage("Weather is sunny", Arg.Any<string>());
        ProviderAdapter.Received().BuildTriggerResponseMessage();
    }

    [Fact]
    public async Task FunctionCall_MultipleCalls_RepliesJoinedWithNewline()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData>
                {
                    new() { FunctionName = "func1", ArgumentsJson = "{}" },
                    new() { FunctionName = "func2", ArgumentsJson = "{}" }
                }
            });

        var callCount = 0;
        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = fc =>
            {
                callCount++;
                return Task.FromResult(new RealtimeAiFunctionCallResult { ReplyMessage = $"Reply{callCount}" });
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Replies should be joined with \n
        ProviderAdapter.Received().BuildTextUserMessage("Reply1\nReply2", Arg.Any<string>());
    }

    [Fact]
    public async Task FunctionCall_OnFunctionCallAsyncNull_FunctionCallsIgnored()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData>
                {
                    new() { FunctionName = "some_func", ArgumentsJson = "{}" }
                }
            });

        // No OnFunctionCallAsync set (null by default)
        var sessionTask = await StartSessionInBackgroundAsync();

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // No text user message should be sent for function call replies
        // Only BuildTurnCompletedMessage sent via turn completed, not BuildTextUserMessage for reply
        // The initial session config is sent, but no text_user messages
        FakeWssClient.SentMessages.ShouldNotContain(m => m.StartsWith("text_user:"));
    }

    [Fact]
    public async Task FunctionCall_ReturnsNull_NoReplySent()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData>
                {
                    new() { FunctionName = "some_func", ArgumentsJson = "{}" }
                }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = _ => Task.FromResult<RealtimeAiFunctionCallResult>(null!);
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        FakeWssClient.SentMessages.ShouldNotContain(m => m.StartsWith("text_user:"));
    }

    [Fact]
    public async Task FunctionCall_ReturnsEmptyReplyMessage_NoReplySent()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData>
                {
                    new() { FunctionName = "some_func", ArgumentsJson = "{}" }
                }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = _ =>
                Task.FromResult(new RealtimeAiFunctionCallResult { ReplyMessage = "" });
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        FakeWssClient.SentMessages.ShouldNotContain(m => m.StartsWith("text_user:"));
    }

    [Fact]
    public async Task FunctionCall_CallbackThrows_ExceptionCaughtTurnNotCompleted()
    {
        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData>
                {
                    new() { FunctionName = "bad_func", ArgumentsJson = "{}" }
                }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = _ => throw new InvalidOperationException("Function call failed");
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Exception is caught by the outer try-catch in OnWssMessageReceivedAsync.
        // Because OnFunctionCallsReceivedAsync throws, the switch case exits early
        // and OnAiTurnCompletedAsync is NEVER called → Round not incremented,
        // BuildTurnCompletedMessage not sent.
        ClientAdapter.DidNotReceive().BuildTurnCompletedMessage(Arg.Any<string>());
    }
}
