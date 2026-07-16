using System.Collections.Concurrent;
using System.Reflection;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.RealtimeAi.Adapters;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAi;

public class RealtimeAiServiceClientMessageTests
{
    [Fact]
    public async Task HandleClientMessageAsync_WhenTextMessage_SendsTextToProvider()
    {
        var engine = Substitute.For<IRealtimeAiConversationEngine>();
        var service = CreateService(engine);

        await InvokeHandleClientMessageAsync(service, """{"type":"RealtimeHttpTextInput","text":"hello"}""");

        await engine.Received(1).SendTextAsync("hello");
        await engine.DidNotReceive().SendAudioChunkAsync(Arg.Any<RealtimeAiWssAudioData>());
    }

    [Fact]
    public async Task HandleClientMessageAsync_WhenRecordingOnlyAudio_WritesRecordingWithoutSendingProviderAudio()
    {
        var engine = Substitute.For<IRealtimeAiConversationEngine>();
        var service = CreateService(engine);

        await InvokeHandleClientMessageAsync(service, """{"type":"RealtimeHttpRecordingAudio","payload":"AQIDBA=="}""");

        GetRecordingBuffer(service).Length.ShouldBe(4);
        await engine.DidNotReceive().SendAudioChunkAsync(Arg.Any<RealtimeAiWssAudioData>());
        await engine.DidNotReceive().SendTextAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleClientMessageAsync_WhenPlainTextMessage_DoesNotSendTextToProvider()
    {
        var engine = Substitute.For<IRealtimeAiConversationEngine>();
        var service = CreateService(engine);

        await InvokeHandleClientMessageAsync(service, """{"text":"hello"}""");

        await engine.DidNotReceive().SendTextAsync(Arg.Any<string>());
        await engine.DidNotReceive().SendAudioChunkAsync(Arg.Any<RealtimeAiWssAudioData>());
    }

    private static RealtimeAiService CreateService(IRealtimeAiConversationEngine engine)
    {
        var service = new RealtimeAiService(
            Substitute.For<IAttachmentService>(),
            Substitute.For<IRealtimeAiSwitcher>(),
            Substitute.For<IInactivityTimerManager>());

        SetPrivateField(service, "_conversationEngine", engine);
        SetPrivateField(service, "_wholeAudioBuffer", new MemoryStream());
        SetPrivateField(service, "_conversationTranscription", new ConcurrentQueue<(AiSpeechAssistantSpeaker, string)>());
        SetPrivateField(service, "_options", new RealtimeSessionOptions
        {
            ConnectionProfile = new RealtimeAiConnectionProfile { ProfileId = "test" },
            InputFormat = RealtimeAiAudioCodec.PCM16
        });

        return service;
    }

    private static async Task InvokeHandleClientMessageAsync(RealtimeAiService service, string message)
    {
        var method = typeof(RealtimeAiService).GetMethod("HandleClientMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        await (Task)method.Invoke(service, [message])!;
    }

    private static MemoryStream GetRecordingBuffer(RealtimeAiService service)
    {
        var field = typeof(RealtimeAiService).GetField("_wholeAudioBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
        field.ShouldNotBeNull();
        return (MemoryStream)field.GetValue(service)!;
    }

    private static void SetPrivateField<T>(RealtimeAiService service, string name, T value)
    {
        var field = typeof(RealtimeAiService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        field.ShouldNotBeNull();
        field.SetValue(service, value);
    }
}
