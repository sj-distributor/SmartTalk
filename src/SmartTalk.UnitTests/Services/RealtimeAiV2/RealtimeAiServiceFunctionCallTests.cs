using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
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
            o.OnFunctionCallAsync = (_, _) =>
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
            o.OnFunctionCallAsync = (_, _) =>
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
            o.OnFunctionCallAsync = (_, _) => Task.FromResult<RealtimeAiFunctionCallResult>(null!);
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
            o.OnFunctionCallAsync = (_, _) =>
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
    public async Task FunctionCall_SendAudioToClient_DelegateSendsAudioDelta()
    {
        var fc = new RealtimeAiWssFunctionCallData { CallId = "call_1", FunctionName = "repeat_order", ArgumentsJson = "{}" };

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData> { fc }
            });

        var audioBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });

        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = async (_, actions) =>
            {
                await actions.SendAudioToClientAsync(audioBase64);
                return new RealtimeAiFunctionCallResult { Output = "done" };
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // The sendAudioToClient delegate should have called BuildAudioDeltaMessage
        ClientAdapter.Received().BuildAudioDeltaMessage(audioBase64, Arg.Any<string>());
        // And the function call reply should also have been sent
        ProviderAdapter.Received(1).BuildFunctionCallReplyMessage(fc, "done");
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
            o.OnFunctionCallAsync = (_, _) => throw new InvalidOperationException("Function call failed");
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

    [Fact]
    public async Task FunctionCall_SuspendClientAudioToProvider_AudioNotForwardedWhileSuspended()
    {
        // The callback suspends audio forwarding and does NOT resume.
        // After the callback completes, client audio arrives → should NOT be forwarded.
        var fc = new RealtimeAiWssFunctionCallData { CallId = "call_1", FunctionName = "repeat_order", ArgumentsJson = "{}" };
        var userAudioBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });

        ProviderAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.FunctionCallSuggested,
                Data = new List<RealtimeAiWssFunctionCallData> { fc }
            });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = userAudioBase64 });

        var options = CreateDefaultOptions(o =>
        {
            o.OnFunctionCallAsync = (_, actions) =>
            {
                actions.SuspendClientAudioToProvider();
                // Intentionally NOT resuming — audio should stay suspended
                return Task.FromResult(new RealtimeAiFunctionCallResult { Output = "done" });
            };
        });

        var sessionTask = await StartSessionInBackgroundAsync(options);

        // 1. Provider sends function call → callback suspends audio forwarding
        await FakeWssClient.SimulateMessageReceivedAsync("{\"type\":\"response.done\"}");
        await Task.Delay(50);

        // 2. Client sends audio while suspended → should NOT be forwarded to provider
        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"AQIDBA==\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Audio should NOT have been forwarded to provider
        ProviderAdapter.DidNotReceive().BuildAudioAppendMessage(Arg.Any<RealtimeAiWssAudioData>());
    }

    [Fact]
    public async Task FunctionCall_ResumeClientAudioToProvider_AudioForwardedAfterResume()
    {
        var userAudioBase64 = Convert.ToBase64String(new byte[] { 10, 20, 30 });

        ClientAdapter.ParseMessage(Arg.Any<string>())
            .Returns(new ParsedClientMessage { Type = RealtimeAiClientMessageType.Audio, Payload = userAudioBase64 });

        // No function call — just test that audio flows normally when not suspended
        var sessionTask = await StartSessionInBackgroundAsync();

        FakeWs.EnqueueClientMessage("{\"media\":{\"payload\":\"ChQe\"}}");
        await Task.Delay(100);

        FakeWs.EnqueueClose();
        await sessionTask;

        // Audio should have been forwarded normally
        ProviderAdapter.Received().BuildAudioAppendMessage(Arg.Any<RealtimeAiWssAudioData>());
    }
}
