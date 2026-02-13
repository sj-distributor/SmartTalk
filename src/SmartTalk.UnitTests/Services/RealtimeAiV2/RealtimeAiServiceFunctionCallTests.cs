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
        var fc = new RealtimeAiWssFunctionCallData { CallId = "call_1", FunctionName = "get_weather", ArgumentsJson = "{\"city\":\"NYC\"}" };

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData> { fc }
            });

        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = _ =>
                Task.FromResult(new RealtimeAiFunctionCallResult { Output = "Weather is sunny" });
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Reply should be sent as function_call_output, not text user message
        ProviderAdapter.Received(1).BuildFunctionCallReplyMessage(fc, "Weather is sunny");
        ProviderAdapter.DidNotReceive().BuildTextUserMessage(Arg.Any<string>(), Arg.Any<string>());
        ProviderAdapter.Received().BuildTriggerResponseMessage();
    }

    [Fact]
    public async Task FunctionCall_MultipleCalls_EachReplyIsSentSeparately()
    {
        var fc1 = new RealtimeAiWssFunctionCallData { CallId = "call_1", FunctionName = "func1", ArgumentsJson = "{}" };
        var fc2 = new RealtimeAiWssFunctionCallData { CallId = "call_2", FunctionName = "func2", ArgumentsJson = "{}" };

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData> { fc1, fc2 }
            });

        var callCount = 0;
        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = _ =>
            {
                callCount++;
                return Task.FromResult(new RealtimeAiFunctionCallResult { Output = $"Reply{callCount}" });
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Each function call reply should be sent separately via BuildFunctionCallReplyMessage
        ProviderAdapter.Received(1).BuildFunctionCallReplyMessage(fc1, "Reply1");
        ProviderAdapter.Received(1).BuildFunctionCallReplyMessage(fc2, "Reply2");
        ProviderAdapter.DidNotReceive().BuildTextUserMessage(Arg.Any<string>(), Arg.Any<string>());
        // Only one response.create trigger at the end
        ProviderAdapter.Received(1).BuildTriggerResponseMessage();
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

        ProviderAdapter.DidNotReceive().BuildFunctionCallReplyMessage(Arg.Any<RealtimeAiWssFunctionCallData>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FunctionCall_ReturnsEmptyOutput_NoReplySent()
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
                Task.FromResult(new RealtimeAiFunctionCallResult { Output = "" });
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        ProviderAdapter.DidNotReceive().BuildFunctionCallReplyMessage(Arg.Any<RealtimeAiWssFunctionCallData>(), Arg.Any<string>());
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
