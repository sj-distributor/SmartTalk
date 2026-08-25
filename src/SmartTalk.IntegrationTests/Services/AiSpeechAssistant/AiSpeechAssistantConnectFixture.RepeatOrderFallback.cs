using System.Text;
using Autofac;
using Mediator.Net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;
using SmartTalk.Core.Settings.MiniMax;
using SmartTalk.IntegrationTests.Mocks;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

/// <summary>
/// CHARACTERIZATION (integration) tests for the RepeatOrder consumer seam that migration step S8
/// reroutes through the generic TTS contract. The existing ShouldProcessRepeatOrderWithMiniMaxVoice test
/// proves the synthesizer was CALLED but with Arg.Any config and never exercises the fallback chain.
/// These pin (1) the exact RealtimeAiTtsConfig that flows to the synthesizer and (2) the empty-text
/// fallback to the gpt-audio voice.
///
/// NOTE: like all tests in this project these require the DB-backed integration harness (DbUp); they are
/// verified in CI, not in a DB-less local run.
/// </summary>
public partial class AiSpeechAssistantConnectFixture
{
    private async Task<int> SeedRepeatOrderAssistantAsync()
    {
        var assistantId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var agent = new Agent { Name = "TestAgent", IsReceiveCall = true, Type = AgentType.Assistant };
            await repository.InsertAsync(agent);

            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "TestAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true,
                CustomRepeatOrderPrompt = "Please repeat the order."
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            assistantId = assistant.Id;

            await repository.InsertAsync(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id });
            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "You are a test assistant.", IsActive = true, Version = "1.0"
            });
            await repository.InsertAsync(new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id, Name = "repeat_order",
                Content = "{\"type\":\"function\",\"name\":\"repeat_order\"}",
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
            });
        });

        return assistantId;
    }

    private static MockWebSocket BuildRepeatOrderTwilioWs(string callSid)
    {
        var twilioWs = new MockWebSocket();
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "start", start = new { callSid, streamSid = "MZ_" + callSid } }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "media", media = new { payload = Convert.ToBase64String(new byte[160]) } }));
        twilioWs.EnqueueMessage(JsonConvert.SerializeObject(new { @event = "stop" }));
        return twilioWs;
    }

    private ProviderMock BuildRepeatOrderProviderMock(string callId)
    {
        var openaiWs = CreateProviderMock();
        openaiWs.EnqueueSendTriggeredMessage(JsonConvert.SerializeObject(new { type = "session.updated" }));
        openaiWs.EnqueueSendTriggeredMessage(JsonConvert.SerializeObject(new
        {
            type = "response.done",
            response = new { output = new[] { new { type = "function_call", name = "repeat_order", call_id = callId, arguments = "{}" } } }
        }));
        return openaiWs;
    }

    private static IConfiguration MiniMaxEnabledConfig(int assistantId) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["MiniMaxTts:Enabled"] = "true",
            ["MiniMaxTts:AssistantId"] = assistantId.ToString(),
            ["MiniMaxTts:ApiKey"] = "test-minimax-key"
        }).Build();

    [Fact]
    public async Task ShouldFlowConfiguredMiniMaxConfigToSynthesizer_OnRepeatOrder()
    {
        var assistantId = await SeedRepeatOrderAssistantAsync();

        var twilioWs = BuildRepeatOrderTwilioWs("CA_REPEAT_CFG");
        var openaiWs = BuildRepeatOrderProviderMock("call_repeat_cfg_001");

        var mockOpenaiClient = Substitute.For<IOpenaiClient>();
        mockOpenaiClient.GenerateTextChatCompletionFromAudioAsync(Arg.Any<BinaryData>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Your order is one large pizza.");

        RealtimeAiTtsConfig? captured = null;
        var mockSynthesizer = Substitute.For<IMiniMaxTtsSynthesizer>();
        mockSynthesizer.SynthesizeStreamingAsync(Arg.Any<RealtimeAiTtsConfig>(), Arg.Any<string>(), Arg.Any<Func<byte[], Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => { captured = ci.Arg<RealtimeAiTtsConfig>(); return ci.Arg<Func<byte[], Task>>()(new byte[] { 1, 2, 3, 4 }); });

        await Run<IMediator>(async mediator => await mediator.SendAsync(new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber, To = TestDidNumber, Host = TestHost, TwilioWebSocket = twilioWs
        }), builder =>
        {
            builder.RegisterInstance(Substitute.For<ISmartTalkBackgroundJobClient>()).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            builder.RegisterInstance(mockOpenaiClient).As<IOpenaiClient>();
            builder.RegisterInstance(mockSynthesizer).As<IMiniMaxTtsSynthesizer>();
            builder.RegisterInstance(new MiniMaxTtsSettings(MiniMaxEnabledConfig(assistantId))).AsSelf().SingleInstance();
            openaiWs.Register(builder);
        });

        // The exact config built by the consumer's gray switch reaches the synthesizer.
        captured.ShouldNotBeNull();
        captured!.ProviderType.ShouldBe(RealtimeAiTtsProviderType.MiniMax);
        captured.ApiKey.ShouldBe("test-minimax-key");
        captured.ProviderSpecificConfig.Keys.OrderBy(k => k)
            .ShouldBe(new[] { "bitrate", "model", "pitch", "source_sample_rate", "speed", "vol" });
    }

    [Fact]
    public async Task ShouldFallBackToGptAudioVoice_WhenMiniMaxRepeatTextEmpty()
    {
        var assistantId = await SeedRepeatOrderAssistantAsync();

        var twilioWs = BuildRepeatOrderTwilioWs("CA_REPEAT_FB");
        var openaiWs = BuildRepeatOrderProviderMock("call_repeat_fb_001");

        var mockOpenaiClient = Substitute.For<IOpenaiClient>();
        mockOpenaiClient.GenerateTextChatCompletionFromAudioAsync(Arg.Any<BinaryData>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("   ");   // empty/whitespace → fallback to gpt-audio voice
        mockOpenaiClient.GenerateAudioChatCompletionAsync(Arg.Any<BinaryData>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1, 2, 3 });

        var mockSynthesizer = Substitute.For<IMiniMaxTtsSynthesizer>();
        var mockFfmpegService = Substitute.For<IFfmpegService>();
        mockFfmpegService.ConvertWavToULawAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 4, 5, 6 });

        await Run<IMediator>(async mediator => await mediator.SendAsync(new ConnectAiSpeechAssistantCommand
        {
            From = TestCallerNumber, To = TestDidNumber, Host = TestHost, TwilioWebSocket = twilioWs
        }), builder =>
        {
            builder.RegisterInstance(Substitute.For<ISmartTalkBackgroundJobClient>()).As<ISmartTalkBackgroundJobClient>();
            builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
            builder.RegisterInstance(mockOpenaiClient).As<IOpenaiClient>();
            builder.RegisterInstance(mockSynthesizer).As<IMiniMaxTtsSynthesizer>();
            builder.RegisterInstance(mockFfmpegService).As<IFfmpegService>();
            builder.RegisterInstance(new MiniMaxTtsSettings(MiniMaxEnabledConfig(assistantId))).AsSelf().SingleInstance();
            openaiWs.Register(builder);
        });

        // Empty repeat text → MiniMax synthesis is skipped and the gpt-audio voice path runs instead.
        await mockOpenaiClient.Received(1).GenerateAudioChatCompletionAsync(
            Arg.Any<BinaryData>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await mockSynthesizer.DidNotReceive().SynthesizeStreamingAsync(
            Arg.Any<RealtimeAiTtsConfig>(), Arg.Any<string>(), Arg.Any<Func<byte[], Task>>(), Arg.Any<CancellationToken>());
    }
}
