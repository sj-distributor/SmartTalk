using AutoMapper;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Core.Settings.Azure;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.WorkWeChat;
using SmartTalk.Core.Settings.ZhiPuAi;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Enums.Sales;
using SmartTalk.Messages.Requests.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeCapabilitiesTests
{
    [Fact]
    public async Task GetKnowledgeCapabilities_WhenStoreIsNotLinked_StillReturnsCapabilities()
    {
        var harness = CapabilityHarness.Create();
        var agent = new Agent { Id = 20, Name = "Sales", Type = AgentType.Sales, RelateId = 300 };
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = 20,
            Name = "Alice",
            IsDisplay = true
        };
        var knowledge = new AiSpeechAssistantKnowledge
        {
            Id = 40,
            AssistantId = 30,
            CreatedDate = DateTimeOffset.UtcNow
        };

        harness.SetupCapabilityGraph(agent, assistant, knowledge, isLink: false);

        var response = await harness.Sut.GetAiSpeechAssistantKnowledgeCapabilitiesAsync(
            new GetAiSpeechAssistantKnowledgeCapabilitiesRequest { StoreId = 10, AgentId = 20 },
            CancellationToken.None);

        var capability = response.Data.Capabilities.ShouldHaveSingleItem();
        capability.KnowledgeId.ShouldBe(40);
        capability.AssistantId.ShouldBe(30);
        capability.AgentId.ShouldBe(20);
    }

    [Fact]
    public async Task GetKnowledgeCapabilities_ReturnsCapabilityFlagsFromAssistantAgentAndTools()
    {
        var harness = CapabilityHarness.Create();
        var agent = new Agent { Id = 20, Name = "Sales", Type = AgentType.Sales, RelateId = 300 };
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = 20,
            Name = "Alice",
            IsDisplay = true,
            ManualRecordWholeAudio = true,
            IsAllowOrderPush = true,
            IsAutoGenerateOrder = true,
            ModelProvider = RealtimeAiProvider.OpenAi
        };
        var knowledge = new AiSpeechAssistantKnowledge
        {
            Id = 40,
            AssistantId = 30,
            Brief = "Knowledge brief",
            CreatedDate = DateTimeOffset.UtcNow
        };

        harness.SetupCapabilityGraph(agent, assistant, knowledge);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Any<List<int>>(),
                Arg.Is<List<string>>(x => x.Contains(OpenAiToolConstants.RepeatOrder) && x.Contains(OpenAiToolConstants.SatisfyOrder)),
                AiSpeechAssistantSessionConfigType.Tool,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantFunctionCall>
            {
                new()
                {
                    AssistantId = 30,
                    Name = OpenAiToolConstants.RepeatOrder,
                    Type = AiSpeechAssistantSessionConfigType.Tool,
                    IsActive = true
                },
                new()
                {
                    AssistantId = 30,
                    Name = OpenAiToolConstants.SatisfyOrder,
                    Type = AiSpeechAssistantSessionConfigType.Tool,
                    IsActive = true
                }
            });
        harness.SalesDataProvider.GetAllSalesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Sales> { new() { Id = 300, Name = "Alice", Type = SalesCallType.CallIn } });

        var response = await harness.Sut.GetAiSpeechAssistantKnowledgeCapabilitiesAsync(
            new GetAiSpeechAssistantKnowledgeCapabilitiesRequest { StoreId = 10, AgentId = 20 },
            CancellationToken.None);

        var capability = response.Data.Capabilities.ShouldHaveSingleItem();
        capability.KnowledgeId.ShouldBe(40);
        capability.AssistantId.ShouldBe(30);
        capability.AgentId.ShouldBe(20);
        capability.KnowledgeName.ShouldBe("Alice");
        capability.AssistantName.ShouldBe("Alice");
        capability.HifoodDataEnabled.ShouldBeTrue();
        capability.RepeatOrderEnabled.ShouldBeTrue();
        capability.OrderPushHifoodEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetKnowledgeCapabilities_WhenAgentIdIsMissing_Throws()
    {
        var harness = CapabilityHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            harness.Sut.GetAiSpeechAssistantKnowledgeCapabilitiesAsync(
                new GetAiSpeechAssistantKnowledgeCapabilitiesRequest { StoreId = 10 },
                CancellationToken.None));

        exception.ParamName.ShouldBe("AgentId");
    }

    [Fact]
    public async Task GetKnowledgeCapabilities_FiltersByAgentId()
    {
        var harness = CapabilityHarness.Create();
        var firstAgent = new Agent { Id = 20, Name = "First" };
        var firstAssistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = 20,
            Name = "Alice",
            IsDisplay = true
        };
        var firstKnowledge = new AiSpeechAssistantKnowledge
        {
            Id = 40,
            AssistantId = 30,
            CreatedDate = DateTimeOffset.UtcNow
        };
        var secondAgent = new Agent { Id = 21, Name = "Second" };
        var secondAssistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 31,
            AgentId = 21,
            Name = "Bob",
            IsDisplay = true
        };
        var secondKnowledge = new AiSpeechAssistantKnowledge
        {
            Id = 41,
            AssistantId = 31,
            CreatedDate = DateTimeOffset.UtcNow
        };

        harness.SetupCapabilityGraphs(true, (firstAgent, firstAssistant, firstKnowledge), (secondAgent, secondAssistant, secondKnowledge));

        var response = await harness.Sut.GetAiSpeechAssistantKnowledgeCapabilitiesAsync(
            new GetAiSpeechAssistantKnowledgeCapabilitiesRequest { StoreId = 10, AgentId = 21 },
            CancellationToken.None);

        var capability = response.Data.Capabilities.ShouldHaveSingleItem();
        capability.AgentId.ShouldBe(21);
        capability.AssistantId.ShouldBe(31);
        capability.KnowledgeId.ShouldBe(41);
    }

    [Fact]
    public async Task GetKnowledgeCapabilities_IgnoresRepeatOrderToolsFromDifferentProvider()
    {
        var harness = CapabilityHarness.Create();
        var agent = new Agent { Id = 20, Name = "Sales", Type = AgentType.Sales, RelateId = 300 };
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = 20,
            Name = "Alice",
            IsDisplay = true,
            ManualRecordWholeAudio = true,
            ModelProvider = RealtimeAiProvider.OpenAi
        };
        var knowledge = new AiSpeechAssistantKnowledge
        {
            Id = 40,
            AssistantId = 30,
            CreatedDate = DateTimeOffset.UtcNow
        };

        harness.SetupCapabilityGraph(agent, assistant, knowledge);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Any<List<int>>(),
                Arg.Is<List<string>>(x => x.Contains(OpenAiToolConstants.RepeatOrder) && x.Contains(OpenAiToolConstants.SatisfyOrder)),
                AiSpeechAssistantSessionConfigType.Tool,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantFunctionCall>
            {
                new()
                {
                    AssistantId = 30,
                    Name = OpenAiToolConstants.RepeatOrder,
                    Type = AiSpeechAssistantSessionConfigType.Tool,
                    ModelProvider = RealtimeAiProvider.ZhiPuAi,
                    IsActive = true
                },
                new()
                {
                    AssistantId = 30,
                    Name = OpenAiToolConstants.SatisfyOrder,
                    Type = AiSpeechAssistantSessionConfigType.Tool,
                    ModelProvider = RealtimeAiProvider.ZhiPuAi,
                    IsActive = true
                }
            });

        var response = await harness.Sut.GetAiSpeechAssistantKnowledgeCapabilitiesAsync(
            new GetAiSpeechAssistantKnowledgeCapabilitiesRequest { StoreId = 10, AgentId = 20 },
            CancellationToken.None);

        response.Data.Capabilities.ShouldHaveSingleItem().RepeatOrderEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAiSpeechAssistant_WhenAgentNoiseReductionDisabled_ShouldCreateInactiveNoiseReductionConfig()
    {
        var harness = CapabilityHarness.Create();
        var agent = new Agent
        {
            Id = 20,
            Type = AgentType.Agent,
            Voice = "alloy",
            WaitInterval = 500,
            IsTransferHuman = false
        };
        var existingAssistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = agent.Id,
            Channel = ((int)AiSpeechAssistantChannel.PhoneChat).ToString(),
            ModelProvider = RealtimeAiProvider.OpenAi
        };

        harness.AgentDataProvider.GetAgentByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);
        harness.AgentDataProvider.GetAgentsByIdsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { agent.Id })),
                Arg.Any<CancellationToken>())
            .Returns([agent]);
        harness.PosDataProvider.GetPosStoreByAgentIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CompanyStore>(null!));
        harness.AiSpeechAssistantDataProvider.GetAgentAssistantsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { agent.Id })),
                null,
                Arg.Any<CancellationToken>())
            .Returns([new AgentAssistant { AgentId = agent.Id, AssistantId = existingAssistant.Id }]);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantsByAgentIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns([existingAssistant]);
        harness.AiSpeechAssistantDataProvider.When(x => x.AddAiSpeechAssistantsAsync(
                Arg.Any<List<Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(),
                true,
                Arg.Any<CancellationToken>()))
            .Do(callInfo => callInfo.Arg<List<Core.Domain.AISpeechAssistant.AiSpeechAssistant>>()[0].Id = 31);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { 31 })),
                RealtimeAiProvider.OpenAi,
                null,
                Arg.Any<CancellationToken>())
            .Returns([]);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { existingAssistant.Id })),
                Arg.Is<List<string>>(x => x.SequenceEqual(new[] { AiSpeechAssistantFunctionCallHelper.InputAudioNoiseReductionName })),
                AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
                RealtimeAiProvider.OpenAi,
                null,
                Arg.Any<CancellationToken>())
            .Returns([
                new AiSpeechAssistantFunctionCall
                {
                    AssistantId = existingAssistant.Id,
                    Name = AiSpeechAssistantFunctionCallHelper.InputAudioNoiseReductionName,
                    Type = AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
                    ModelProvider = RealtimeAiProvider.OpenAi,
                    IsActive = false
                }
            ]);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Is<List<int>>(x => x.SequenceEqual(new[] { 31 })),
                Arg.Is<List<string>>(x => x.SequenceEqual(new[] { AiSpeechAssistantFunctionCallHelper.InputAudioNoiseReductionName })),
                AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
                RealtimeAiProvider.OpenAi,
                null,
                Arg.Any<CancellationToken>())
            .Returns([]);

        await harness.Sut.AddAiSpeechAssistantAsync(new AddAiSpeechAssistantCommand
        {
            AgentId = agent.Id,
            AgentType = AgentType.Agent,
            AssistantName = "New Assistant",
            Channels = [AiSpeechAssistantChannel.PhoneChat],
            Json = "{}",
            Details = []
        }, CancellationToken.None);

        await harness.AiSpeechAssistantDataProvider.Received(1).AddAiSpeechAssistantFunctionCallsAsync(
            Arg.Is<List<AiSpeechAssistantFunctionCall>>(calls =>
                calls.Count == 1 &&
                calls[0].AssistantId == 31 &&
                calls[0].Name == AiSpeechAssistantFunctionCallHelper.InputAudioNoiseReductionName &&
                calls[0].Type == AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction &&
                calls[0].ModelProvider == RealtimeAiProvider.OpenAi &&
                !calls[0].IsActive),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateKnowledgeCapabilities_WhenRepeatOrderEnabled_AddsToolsAndUpdatesAssistant()
    {
        var harness = CapabilityHarness.Create();
        var agent = new Agent { Id = 20, Name = "Agent", Type = AgentType.Agent, RelateId = 30 };
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = 20,
            Name = "Alice",
            IsDisplay = true,
            ManualRecordWholeAudio = false,
            ModelLanguage = "En",
            ModelProvider = RealtimeAiProvider.OpenAi
        };
        var knowledge = new AiSpeechAssistantKnowledge
        {
            Id = 40,
            AssistantId = 30,
            Brief = "Knowledge brief",
            CreatedDate = DateTimeOffset.UtcNow
        };
        var addedFunctionCalls = new List<AiSpeechAssistantFunctionCall>();

        harness.SetupCapabilityGraph(agent, assistant, knowledge);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Any<List<int>>(),
                Arg.Any<List<string>>(),
                AiSpeechAssistantSessionConfigType.Tool,
                Arg.Any<RealtimeAiProvider?>(),
                null,
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantFunctionCall>());
        harness.AiSpeechAssistantDataProvider
            .AddAiSpeechAssistantFunctionCallsAsync(
                Arg.Do<List<AiSpeechAssistantFunctionCall>>(x => addedFunctionCalls.AddRange(x)),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await harness.Sut.UpdateAiSpeechAssistantKnowledgeCapabilitiesAsync(
            new UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand
            {
                StoreId = 10,
                Items =
                [
                    new UpdateAiSpeechAssistantKnowledgeCapabilityDto
                    {
                        AssistantId = 30,
                        RepeatOrderEnabled = true
                    }
                ]
            },
            CancellationToken.None);

        assistant.ManualRecordWholeAudio.ShouldBeTrue();
        assistant.CustomRepeatOrderPrompt.ShouldNotBeNullOrWhiteSpace();
        addedFunctionCalls.Select(x => x.Name).OrderBy(x => x).ShouldBe([
            OpenAiToolConstants.RepeatOrder,
            OpenAiToolConstants.SatisfyOrder
        ]);
        addedFunctionCalls.ShouldAllBe(x =>
            x.AssistantId == 30 &&
            x.Type == AiSpeechAssistantSessionConfigType.Tool &&
            x.ModelProvider == RealtimeAiProvider.OpenAi &&
            x.IsActive);
        await harness.AiSpeechAssistantDataProvider.Received(1)
            .UpdateAiSpeechAssistantsAsync(
                Arg.Is<List<Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(x => x.Single() == assistant),
                true,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateKnowledgeCapabilities_ShouldNotRequireStoreBinding()
    {
        var harness = CapabilityHarness.Create([]);
        var agent = new Agent { Id = 20, Name = "Agent", Type = AgentType.Agent, RelateId = 30 };
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = 20,
            Name = "Alice",
            IsDisplay = true,
            ManualRecordWholeAudio = false,
            ModelLanguage = "En",
            ModelProvider = RealtimeAiProvider.OpenAi
        };
        var knowledge = new AiSpeechAssistantKnowledge
        {
            Id = 40,
            AssistantId = 30,
            CreatedDate = DateTimeOffset.UtcNow
        };

        harness.SetupCapabilityGraph(agent, assistant, knowledge);
        harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                Arg.Any<List<int>>(),
                Arg.Any<List<string>>(),
                AiSpeechAssistantSessionConfigType.Tool,
                Arg.Any<RealtimeAiProvider?>(),
                null,
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantFunctionCall>());

        await harness.Sut.UpdateAiSpeechAssistantKnowledgeCapabilitiesAsync(
            new UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand
            {
                StoreId = 10,
                Items =
                [
                    new UpdateAiSpeechAssistantKnowledgeCapabilityDto
                    {
                        AssistantId = 30,
                        RepeatOrderEnabled = true
                    }
                ]
            },
            CancellationToken.None);

        assistant.ManualRecordWholeAudio.ShouldBeTrue();
        await harness.PosDataProvider.DidNotReceive()
            .GetPosStoreUsersByUserIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateKnowledgeCapabilities_WhenHifoodDisabled_RemovesPromptAndRestoresSalesAgent()
    {
        var harness = CapabilityHarness.Create();
        var agent = new Agent { Id = 20, Name = "Agent", Type = AgentType.Sales, RelateId = 300 };
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 30,
            AgentId = 20,
            Name = "Alice",
            IsDisplay = true,
            CustomRecordAnalyzePrompt = "Keep this line" + Environment.NewLine + "Use #{customer_items}",
            ModelProvider = RealtimeAiProvider.OpenAi
        };
        var knowledge = new AiSpeechAssistantKnowledge
        {
            Id = 40,
            AssistantId = 30,
            CreatedDate = DateTimeOffset.UtcNow
        };

        harness.SetupCapabilityGraph(agent, assistant, knowledge);
        harness.SalesDataProvider.GetAllSalesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Sales> { new() { Id = 300, Name = "Alice", Type = SalesCallType.CallIn } });

        await harness.Sut.UpdateAiSpeechAssistantKnowledgeCapabilitiesAsync(
            new UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand
            {
                StoreId = 10,
                Items =
                [
                    new UpdateAiSpeechAssistantKnowledgeCapabilityDto
                    {
                        AssistantId = 30,
                        HifoodDataEnabled = false
                    }
                ]
            },
            CancellationToken.None);

        assistant.CustomRecordAnalyzePrompt.ShouldBe("Keep this line");
        agent.Type.ShouldBe(AgentType.Agent);
        agent.RelateId.ShouldBe(30);
        await harness.AgentDataProvider.Received(1)
            .UpdateAgentsAsync(
                Arg.Is<List<Agent>>(x => x.Single() == agent),
                true,
                Arg.Any<CancellationToken>());
        await harness.AiSpeechAssistantDataProvider.Received(1)
            .UpdateAiSpeechAssistantsAsync(
                Arg.Is<List<Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(x => x.Single() == assistant),
                true,
                Arg.Any<CancellationToken>());
    }

    private sealed class CapabilityHarness
    {
        private const int CurrentUserId = 9;

        public AiSpeechAssistantService Sut { get; private set; } = null!;

        public ICurrentUser CurrentUser { get; private set; } = null!;

        public IPosDataProvider PosDataProvider { get; private set; } = null!;

        public IAgentDataProvider AgentDataProvider { get; private set; } = null!;

        public IAiSpeechAssistantDataProvider AiSpeechAssistantDataProvider { get; private set; } = null!;

        public ISalesDataProvider SalesDataProvider { get; private set; } = null!;

        public static CapabilityHarness Create(List<StoreUser>? storeUsers = null)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Azure:ApiKey"] = "test",
                ["OpenAi:BaseUrl"] = "https://example.com",
                ["OpenAi:ApiKey"] = "test",
                ["OpenAi:Organization"] = "test",
                ["ZhiPuAi:ApiKey"] = "test",
                ["WorkWeChat:Key"] = "test"
            }).Build();

            var currentUser = Substitute.For<ICurrentUser>();
            currentUser.Id.Returns(CurrentUserId);
            currentUser.Name.Returns("unit-test");

            var posDataProvider = Substitute.For<IPosDataProvider>();
            posDataProvider.GetPosStoreUsersByUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
                .Returns(storeUsers ?? [new StoreUser { UserId = CurrentUserId, StoreId = 10 }]);

            var harness = new CapabilityHarness
            {
                CurrentUser = currentUser,
                PosDataProvider = posDataProvider,
                AgentDataProvider = Substitute.For<IAgentDataProvider>(),
                AiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>(),
                SalesDataProvider = Substitute.For<ISalesDataProvider>()
            };

            harness.Sut = new AiSpeechAssistantService(
                Substitute.For<IClock>(),
                Substitute.For<IMapper>(),
                Substitute.For<ICrmClient>(),
                currentUser,
                new AzureSetting(configuration),
                Substitute.For<ICacheManager>(),
                Substitute.For<IOpenaiClient>(),
                Substitute.For<IFfmpegService>(),
                new OpenAiSettings(configuration),
                Substitute.For<ISmartiesClient>(),
                new ZhiPuAiSettings(configuration),
                Substitute.For<IRedisSafeRunner>(),
                posDataProvider,
                Substitute.For<IPosUtilService>(),
                Substitute.For<IPhoneOrderService>(),
                harness.AgentDataProvider,
                Substitute.For<IAttachmentService>(),
                harness.SalesDataProvider,
                Substitute.For<ISpeechToTextService>(),
                Substitute.For<IFileTextExtractor>(),
                new WorkWeChatKeySetting(configuration),
                Substitute.For<ISmartTalkHttpClientFactory>(),
                Substitute.For<IRestaurantDataProvider>(),
                Substitute.For<IPhoneOrderDataProvider>(),
                Substitute.For<IInactivityTimerManager>(),
                Substitute.For<ISmartTalkBackgroundJobClient>(),
                Substitute.For<ITwilioService>(),
                harness.AiSpeechAssistantDataProvider,
                Substitute.For<IAiSpeechAssistantKnowledgePromptService>(),
                Substitute.For<IKnowledgeScenarioDataProvider>());

            harness.AiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
                    Arg.Any<List<int>>(),
                    Arg.Any<List<string>>(),
                    Arg.Any<AiSpeechAssistantSessionConfigType?>(),
                    null,
                    null,
                    Arg.Any<CancellationToken>())
                .Returns(new List<AiSpeechAssistantFunctionCall>());
            harness.SalesDataProvider.GetAllSalesAsync(Arg.Any<CancellationToken>())
                .Returns(new List<Sales>());

            return harness;
        }

        public void SetupStore(CompanyStore store)
        {
            PosDataProvider.GetPosCompanyStoreAsync(null, store.Id, null, null, Arg.Any<CancellationToken>())
                .Returns(store);
        }

        public void SetupCapabilityGraph(
            Agent agent,
            Core.Domain.AISpeechAssistant.AiSpeechAssistant assistant,
            AiSpeechAssistantKnowledge knowledge,
            bool isLink = true)
        {
            SetupCapabilityGraphs(isLink, (agent, assistant, knowledge));
        }

        public void SetupCapabilityGraphs(
            bool isLink,
            params (Agent Agent, Core.Domain.AISpeechAssistant.AiSpeechAssistant Assistant, AiSpeechAssistantKnowledge Knowledge)[] records)
        {
            SetupStore(new CompanyStore { Id = 10, IsLink = isLink });
            var agentIds = records.Select(x => x.Agent.Id).Distinct().ToList();
            var assistantIds = records.Select(x => x.Assistant.Id).Distinct().ToList();

            PosDataProvider.GetPosAgentsAsync(
                    Arg.Is<List<int>>(x => x.SequenceEqual(new[] { 10 })),
                    null,
                    Arg.Any<CancellationToken>())
                .Returns(agentIds.Select(agentId => new PosAgent { StoreId = 10, AgentId = agentId }).ToList());
            AgentDataProvider.GetAgentsByIdsAsync(
                    Arg.Is<List<int>>(x => x.SequenceEqual(agentIds)),
                    Arg.Any<CancellationToken>())
                .Returns(records
                    .Select(x => x.Agent)
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList());
            AiSpeechAssistantDataProvider.GetAgentAssistantsAsync(
                    Arg.Is<List<int>>(x => x.SequenceEqual(agentIds)),
                    null,
                    Arg.Any<CancellationToken>())
                .Returns(records
                    .Select(x => new AgentAssistant { AgentId = x.Agent.Id, AssistantId = x.Assistant.Id })
                    .ToList());
            AiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdsAsync(
                    Arg.Is<List<int>>(x => x.SequenceEqual(assistantIds)),
                    Arg.Any<CancellationToken>())
                .Returns(records
                    .Select(x => x.Assistant)
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList());
            AiSpeechAssistantDataProvider.GetAiSpeechAssistantActiveKnowledgesAsync(
                    Arg.Is<List<int>>(x => x.SequenceEqual(assistantIds)),
                    Arg.Any<CancellationToken>())
                .Returns(records.Select(x => x.Knowledge).ToList());
        }
    }
}
