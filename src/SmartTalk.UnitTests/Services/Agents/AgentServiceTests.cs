using AutoMapper;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Mappings;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Requests.Agent;
using Xunit;
using AiAssistant = SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant;

namespace SmartTalk.UnitTests.Services.Agents;

public class AgentServiceTests
{
    [Fact]
    public async Task GetSurfaceAgentsAsync_WhenNoiseReductionConfigured_ShouldReturnTrue()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AgentMapping>();
            cfg.AddProfile<AiSpeechAssistantMapping>();
        }).CreateMapper();
        var agentDataProvider = Substitute.For<IAgentDataProvider>();
        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        var posDataProvider = Substitute.For<IPosDataProvider>();
        var service = new AgentService(
            mapper,
            Substitute.For<ICurrentUser>(),
            posDataProvider,
            agentDataProvider,
            Substitute.For<IRestaurantDataProvider>(),
            Substitute.For<IAccountDataProvider>(),
            aiSpeechAssistantDataProvider,
            Substitute.For<ISmartTalkBackgroundJobClient>());
        var agent = new Agent
        {
            Id = 20,
            Type = AgentType.Agent,
            Channel = AiSpeechAssistantChannel.PhoneChat,
            Voice = "alloy",
            WaitInterval = 500,
            IsTransferHuman = false
        };
        var assistant = new AiAssistant
        {
            Id = 30,
            AgentId = agent.Id,
            Channel = ((int)AiSpeechAssistantChannel.PhoneChat).ToString(),
            ModelProvider = RealtimeAiProvider.OpenAi
        };

        posDataProvider.GetPosAgentsAsync(Arg.Is<List<int>>(x => x.SequenceEqual(new[] { 1 })), null, Arg.Any<CancellationToken>())
            .Returns([new PosAgent { AgentId = agent.Id, StoreId = 1 }]);
        agentDataProvider.GetAgentsPagingAsync(
                1,
                10,
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { agent.Id })),
                null,
                Arg.Any<CancellationToken>())
            .Returns((1, [agent]));
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByAgentIdsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { agent.Id })),
                Arg.Any<CancellationToken>())
            .Returns([(new AgentAssistant { AgentId = agent.Id, AssistantId = assistant.Id }, assistant)]);
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { assistant.Id })),
                Arg.Is<List<string>>(x => x.SequenceEqual(new[] { "input_audio_noise_reduction" })),
                AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
                null,
                true,
                Arg.Any<CancellationToken>())
            .Returns([
                new AiSpeechAssistantFunctionCall
                {
                    AssistantId = assistant.Id,
                    Name = "input_audio_noise_reduction",
                    Type = AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
                    ModelProvider = RealtimeAiProvider.OpenAi,
                    IsActive = true
                }
            ]);

        var response = await service.GetSurfaceAgentsAsync(
            new GetSurfaceAgentsRequest
            {
                StoreId = 1,
                PageIndex = 1,
                PageSize = 10
            },
            CancellationToken.None);

        response.Data.Count.ShouldBe(1);
        response.Data.Agents.ShouldHaveSingleItem();
        response.Data.Agents[0].Assistants.ShouldHaveSingleItem();
        response.Data.Agents[0].Assistants[0].PhoneNoiseReductionEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAgentAsync_WhenPhoneNoiseReductionEnabled_ShouldCreateNoiseReductionConfig()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AgentMapping>();
            cfg.AddProfile<AiSpeechAssistantMapping>();
        }).CreateMapper();
        var agentDataProvider = Substitute.For<IAgentDataProvider>();
        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        var service = new AgentService(
            mapper,
            Substitute.For<ICurrentUser>(),
            Substitute.For<IPosDataProvider>(),
            agentDataProvider,
            Substitute.For<IRestaurantDataProvider>(),
            Substitute.For<IAccountDataProvider>(),
            aiSpeechAssistantDataProvider,
            Substitute.For<ISmartTalkBackgroundJobClient>());
        var agent = new Agent
        {
            Id = 20,
            Type = AgentType.Agent,
            Channel = AiSpeechAssistantChannel.PhoneChat,
            Voice = "alloy",
            WaitInterval = 500,
            IsTransferHuman = false
        };
        var assistant = new AiAssistant
        {
            Id = 30,
            AgentId = agent.Id,
            Channel = ((int)AiSpeechAssistantChannel.PhoneChat).ToString(),
            ModelProvider = RealtimeAiProvider.OpenAi
        };

        agentDataProvider.GetAgentByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantByAgentIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<AiAssistant>(null!));
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByAgentIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns([assistant]);
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactsAsync(
                Arg.Any<List<int>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { assistant.Id })),
                Arg.Is<List<string>>(x => x.SequenceEqual(new[] { "input_audio_noise_reduction" })),
                AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
                RealtimeAiProvider.OpenAi,
                null,
                Arg.Any<CancellationToken>())
            .Returns([]);
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
                Arg.Any<List<int>>(),
                RealtimeAiProvider.OpenAi,
                null,
                Arg.Any<CancellationToken>())
            .Returns([
                new AiSpeechAssistantFunctionCall
                {
                    AssistantId = assistant.Id,
                    Name = "transfer_call",
                    Type = AiSpeechAssistantSessionConfigType.Tool,
                    ModelProvider = RealtimeAiProvider.OpenAi
                },
                new AiSpeechAssistantFunctionCall
                {
                    AssistantId = assistant.Id,
                    Name = "turn_detection",
                    Content = """{"type":"semantic_vad"}""",
                    Type = AiSpeechAssistantSessionConfigType.TurnDirection,
                    ModelProvider = RealtimeAiProvider.OpenAi
                }
            ]);

        await service.UpdateAgentAsync(
            new UpdateAgentCommand
            {
                AgentId = agent.Id,
                Name = "Agent",
                Brief = "Brief",
                Channel = AiSpeechAssistantChannel.PhoneChat,
                Voice = "verse",
                WaitInterval = 800,
                IsTransferHuman = false,
                TransferCallNumber = string.Empty,
                ServiceHours = "[]",
                PhoneNoiseReductionEnabled = true
            },
            CancellationToken.None);

        await aiSpeechAssistantDataProvider.Received(1).AddAiSpeechAssistantFunctionCallsAsync(
            Arg.Is<List<AiSpeechAssistantFunctionCall>>(calls =>
                calls.Count == 1 &&
                calls[0].AssistantId == assistant.Id &&
                calls[0].Name == "input_audio_noise_reduction" &&
                calls[0].Type == AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction &&
                calls[0].ModelProvider == RealtimeAiProvider.OpenAi &&
                calls[0].IsActive &&
                JObject.Parse(calls[0].Content)["type"]!.Value<string>() == "near_field"),
            true,
            Arg.Any<CancellationToken>());
    }
}
