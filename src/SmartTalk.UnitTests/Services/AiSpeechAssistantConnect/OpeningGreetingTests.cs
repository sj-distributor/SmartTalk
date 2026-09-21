using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

public class OpeningGreetingTests
{
    [Fact]
    public async Task SendOpeningGreetingOnceAsync_RepeatedReadyEvents_SendsGreetingOnce()
    {
        var context = CreateContext("Hello");
        var sendCount = 0;
        var releaseFirstSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new RealtimeAiSessionActions
        {
            SendTextToProviderAsync = async message =>
            {
                Assert.Equal("Greet the user with: 'Hello'", message);
                Interlocked.Increment(ref sendCount);
                await releaseFirstSend.Task;
            }
        };

        var firstReadyEvent = AiSpeechAssistantConnectService.SendOpeningGreetingOnceAsync(context, actions);
        var duplicateReadyEvent = AiSpeechAssistantConnectService.SendOpeningGreetingOnceAsync(context, actions);

        await duplicateReadyEvent;
        releaseFirstSend.SetResult();
        await firstReadyEvent;

        Assert.Equal(1, sendCount);
    }

    [Fact]
    public async Task SendOpeningGreetingOnceAsync_EmptyGreeting_DoesNotConsumeTrigger()
    {
        var context = CreateContext(string.Empty);
        var sendCount = 0;
        var actions = new RealtimeAiSessionActions
        {
            SendTextToProviderAsync = _ =>
            {
                Interlocked.Increment(ref sendCount);
                return Task.CompletedTask;
            }
        };

        await AiSpeechAssistantConnectService.SendOpeningGreetingOnceAsync(context, actions);
        context.Knowledge.Greetings = "Hello";
        await AiSpeechAssistantConnectService.SendOpeningGreetingOnceAsync(context, actions);

        Assert.Equal(1, sendCount);
    }

    [Fact]
    public async Task SendOpeningGreetingOnceAsync_NewCallContext_SendsGreetingForEachCall()
    {
        var sendCount = 0;
        var actions = new RealtimeAiSessionActions
        {
            SendTextToProviderAsync = _ =>
            {
                Interlocked.Increment(ref sendCount);
                return Task.CompletedTask;
            }
        };

        await AiSpeechAssistantConnectService.SendOpeningGreetingOnceAsync(CreateContext("Hello"), actions);
        await AiSpeechAssistantConnectService.SendOpeningGreetingOnceAsync(CreateContext("Hello"), actions);

        Assert.Equal(2, sendCount);
    }

    private static AiSpeechAssistantConnectContext CreateContext(string greeting) => new()
    {
        Knowledge = new AiSpeechAssistantKnowledgeDto { Greetings = greeting }
    };
}
