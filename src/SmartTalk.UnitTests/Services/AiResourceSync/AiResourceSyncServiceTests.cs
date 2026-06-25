using System.Linq.Expressions;
using Mediator.Net;
using Mediator.Net.Context;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Handlers.EventHandlers.AiResourceSync;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Events.AiResourceSync;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiResourceSync;

public class AiResourceSyncServiceTests
{
    [Fact]
    public async Task AiResourceSyncEventHandler_EnqueuesBackgroundExecution()
    {
        var backgroundJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();
        Expression<Func<IAiResourceSyncProcessJobService, Task>>? queuedExpression = null;
        backgroundJobClient
            .Enqueue<IAiResourceSyncProcessJobService>(Arg.Do<Expression<Func<IAiResourceSyncProcessJobService, Task>>>(x => queuedExpression = x), Arg.Any<string>())
            .Returns("job-1");

        var handler = new AiResourceSyncEventHandler(backgroundJobClient);
        var context = Substitute.For<IReceiveContext<AiResourceSyncEvent>>();
        context.Message.Returns(new AiResourceSyncEvent
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        });

        await handler.Handle(context, CancellationToken.None);

        backgroundJobClient.Received(1).Enqueue(Arg.Any<Expression<Func<IAiResourceSyncProcessJobService, Task>>>(), Arg.Any<string>());
        Assert.NotNull(queuedExpression);
        var methodCall = Assert.IsAssignableFrom<MethodCallExpression>(queuedExpression.Body);
        Assert.Equal(nameof(IAiResourceSyncProcessJobService.ExecuteSyncCrmSalesAutoCreateAsync), methodCall.Method.Name);
    }

    [Fact]
    public async Task ExecuteSyncCrmSalesAutoCreateAsync_CreatesAssistantWithKnowledgeDetailsFromSceneItems()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(
                Arg.Any<int>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((
                new List<CrmSalesAutoSyncCustomerDto>
                {
                    new()
                    {
                        CustomerId = "100",
                        SalesName = "Alice",
                        SalesGroup = "GroupA",
                        Language = "English"
                    }
                },
                1
            ));

        var mediator = Substitute.For<IMediator>();
        mediator.SendAsync<CreateCompanyStoreCommand, CreateCompanyStoreResponse>(
                Arg.Any<CreateCompanyStoreCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateCompanyStoreResponse { Data = new CompanyStoreDto { Id = 10 } });
        mediator.SendAsync<AddAgentCommand, AddAgentResponse>(
                Arg.Any<AddAgentCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AddAgentResponse { Data = new AgentDto { Id = 201 } });
        mediator.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
                Arg.Any<AddAiSpeechAssistantCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AddAiSpeechAssistantResponse { Data = new AiSpeechAssistantDto { Id = 101 } });

        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });
        posDataProvider.GetPosCompanyStoresAsync(
                Arg.Any<List<int>>(), Arg.Any<List<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.Pos.CompanyStore>());
        posDataProvider.GetPosCompanyStoreAsync(null, 10, null, null, Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.CompanyStore
            {
                Id = 10,
                CompanyId = 1,
                CreatedDate = DateTimeOffset.UtcNow,
                Names = "{\"en\":{\"name\":\"Alice GroupA\"},\"cn\":{\"name\":\"Alice GroupA\"}}"
            });
        posDataProvider.GetPosAgentsAsync(Arg.Any<List<int>>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<PosAgent>
            {
                new() { StoreId = 10, AgentId = 201, CreatedDate = DateTimeOffset.UtcNow }
            });

        var agentDataProvider = Substitute.For<IAgentDataProvider>();
        agentDataProvider.GetAgentsByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Agent>
            {
                new()
                {
                    Id = 201,
                    Name = "Alice GroupA",
                    Type = AgentType.Sales,
                    SourceSystem = AgentSourceSystem.CrmAutoSync,
                    CreatedDate = DateTimeOffset.UtcNow
                }
            });
        agentDataProvider.GetAgentByIdAsync(201, Arg.Any<CancellationToken>())
            .Returns(new Agent
            {
                Id = 201,
                Name = "Alice GroupA",
                Type = AgentType.Sales,
                SourceSystem = AgentSourceSystem.CrmAutoSync,
                CreatedDate = DateTimeOffset.UtcNow
            });

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<CrmAutoSyncAssistantLocationDto>());
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(10, "100", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>(null),
                Task.FromResult(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
                {
                    Id = 101,
                    AgentId = 201,
                    Name = "100"
                }));
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(101, Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Id = 101,
                AgentId = 201,
                Name = "100"
            });
        aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync(
                Arg.Any<List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var knowledgeScenarioDataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(1, null, null, true, Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneLanguageMapping>
            {
                new() { CompanyId = 1, SceneId = 500, Language = AutoAddLanguage.English, CreatedAt = DateTimeOffset.UtcNow }
            });
        knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeScene>
            {
                new() { Id = 500, Status = KnowledgeSceneStatus.Published }
            });
        knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdAsync(500, Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneItem>
            {
                new()
                {
                    Id = 901,
                    SceneId = 500,
                    Name = "Scene item example",
                    Type = KnowledgeSceneItemType.FAQ,
                    Content = "scene item content",
                    FileName = "scene-item.txt"
                }
            });
        var salesDataProvider = Substitute.For<ISalesDataProvider>();
        salesDataProvider.AddCrmSalesAutoSyncRunAsync(Arg.Any<SmartTalk.Core.Domain.Sales.CrmSalesAutoSyncRun>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var redisSafeRunner = Substitute.For<IRedisSafeRunner>();
        redisSafeRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>()());

        var sut = CreateSut(
            mediator: mediator,
            crmClient: crmClient,
            agentDataProvider: agentDataProvider,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            knowledgeScenarioDataProvider: knowledgeScenarioDataProvider,
            salesDataProvider: salesDataProvider,
            redisSafeRunner: redisSafeRunner);
        
        await sut.SyncInternalAsync(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        }, new List<CrmSalesAutoSyncCustomerDto>(), CancellationToken.None);

        await mediator.Received(1).SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
            Arg.Is<AddAiSpeechAssistantCommand>(x =>
                x.AssistantName == "100" &&
                x.CreatedBy == 888 &&
                x.Greetings == "Hello" &&
                x.Json == "{}" &&
                x.Details.Count == 1 &&
                x.Details[0].KnowledgeName == "Scene item example" &&
                x.Details[0].FormatType == AiSpeechAssistantKonwledgeFormatType.FAQ &&
                x.Details[0].Content == "scene item content" &&
                x.Details[0].FileName == "scene-item.txt"),
            Arg.Any<CancellationToken>());

        await mediator.Received(1).SendAsync<CreateCompanyStoreCommand, CreateCompanyStoreResponse>(
            Arg.Is<CreateCompanyStoreCommand>(x =>
                x.CompanyId == 1 &&
                x.CreatedBy == 888),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncInternalAsync_WhenOmeHasNoKnowledgeSceneMapping_ShouldThrowAndStop()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(
                Arg.Any<int>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((
                new List<CrmSalesAutoSyncCustomerDto>
                {
                    new()
                    {
                        CustomerId = "100",
                        SalesName = "Alice",
                        SalesGroup = "GroupA",
                        Language = "English"
                    }
                },
                1
            ));

        var mediator = Substitute.For<IMediator>();
        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });
        posDataProvider.GetPosCompanyStoresAsync(
                Arg.Any<List<int>>(), Arg.Any<List<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.Pos.CompanyStore>());

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<CrmAutoSyncAssistantLocationDto>());

        var knowledgeScenarioDataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(1, null, null, true, Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneLanguageMapping>());

        var salesDataProvider = Substitute.For<ISalesDataProvider>();
        salesDataProvider.AddCrmSalesAutoSyncRunAsync(Arg.Any<SmartTalk.Core.Domain.Sales.CrmSalesAutoSyncRun>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var redisSafeRunner = Substitute.For<IRedisSafeRunner>();
        redisSafeRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<Agent>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<Agent>>>()());
        redisSafeRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>()());

        var sut = CreateSut(
            mediator: mediator,
            crmClient: crmClient,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            knowledgeScenarioDataProvider: knowledgeScenarioDataProvider,
            salesDataProvider: salesDataProvider,
            redisSafeRunner: redisSafeRunner);

        var ex = await Assert.ThrowsAsync<Exception>(() => sut.SyncInternalAsync(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        }, new List<CrmSalesAutoSyncCustomerDto>(), CancellationToken.None));

        Assert.Contains("has no active knowledge scene mapping", ex.Message);
        await mediator.DidNotReceive().SendAsync<AddAgentCommand, AddAgentResponse>(
            Arg.Any<AddAgentCommand>(),
            Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
            Arg.Any<AddAiSpeechAssistantCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncInternalAsync_ReusesExistingCrmAutoSyncAgentAndAssistantWithoutCreatingDuplicates()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(
                Arg.Any<int>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((
                new List<CrmSalesAutoSyncCustomerDto>
                {
                    new()
                    {
                        CustomerId = "116399",
                        SalesName = "CHRISTINE.C",
                        SalesGroup = "057",
                        Language = "English"
                    }
                },
                1
            ));

        var mediator = Substitute.For<IMediator>();
        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });
        posDataProvider.GetPosCompanyStoresAsync(
                Arg.Any<List<int>>(), Arg.Any<List<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.Pos.CompanyStore>
            {
                new()
                {
                    Id = 10,
                    CompanyId = 1,
                    CreatedDate = DateTimeOffset.UtcNow,
                    Names = "{\"en\":{\"name\":\"CHRISTINE.C 057\"},\"cn\":{\"name\":\"CHRISTINE.C 057\"}}"
                }
            });
        posDataProvider.GetPosAgentsAsync(Arg.Any<List<int>>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<PosAgent>
            {
                new() { StoreId = 10, AgentId = 10347, CreatedDate = DateTimeOffset.UtcNow }
            });

        var agentDataProvider = Substitute.For<IAgentDataProvider>();
        agentDataProvider.GetAgentsByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Agent>
            {
                new()
                {
                    Id = 10347,
                    Name = "CHRISTINE.C 057",
                    Type = AgentType.Sales,
                    SourceSystem = AgentSourceSystem.CrmAutoSync,
                    CreatedDate = DateTimeOffset.UtcNow
                }
            });

        var existingAssistant = new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 1141,
            AgentId = 10347,
            Name = "116399",
            CreatedDate = DateTimeOffset.UtcNow
        };

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<CrmAutoSyncAssistantLocationDto>
            {
                new()
                {
                    AssistantId = 1141,
                    StoreId = 10,
                    AgentId = 10347,
                    Name = "116399"
                }
            });
        aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync(
                Arg.Any<List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var knowledgeScenarioDataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(1, null, null, true, Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneLanguageMapping>
            {
                new() { CompanyId = 1, SceneId = 500, Language = AutoAddLanguage.English, CreatedAt = DateTimeOffset.UtcNow }
            });
        knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeScene>
            {
                new() { Id = 500, Status = KnowledgeSceneStatus.Published }
            });
        knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdAsync(500, Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneItem>
            {
                new()
                {
                    Id = 901,
                    SceneId = 500,
                    Name = "Scene item example",
                    Type = KnowledgeSceneItemType.FAQ,
                    Content = "scene item content",
                    FileName = "scene-item.txt"
                }
            });

        var redisSafeRunner = Substitute.For<IRedisSafeRunner>();
        redisSafeRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<Agent>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<Agent>>>()());
        redisSafeRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>()());

        var salesDataProvider = Substitute.For<ISalesDataProvider>();
        salesDataProvider.AddCrmSalesAutoSyncRunAsync(Arg.Any<SmartTalk.Core.Domain.Sales.CrmSalesAutoSyncRun>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            mediator: mediator,
            crmClient: crmClient,
            agentDataProvider: agentDataProvider,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            knowledgeScenarioDataProvider: knowledgeScenarioDataProvider,
            salesDataProvider: salesDataProvider,
            redisSafeRunner: redisSafeRunner);

        await sut.SyncInternalAsync(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        }, new List<CrmSalesAutoSyncCustomerDto>(), CancellationToken.None);

        await mediator.DidNotReceive().SendAsync<AddAgentCommand, AddAgentResponse>(
            Arg.Any<AddAgentCommand>(),
            Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
            Arg.Any<AddAiSpeechAssistantCommand>(),
            Arg.Any<CancellationToken>());
    }

    private static AiResourceSyncService CreateSut(
        IMediator? mediator = null,
        ICrmClient? crmClient = null,
        IAgentDataProvider? agentDataProvider = null,
        IPosDataProvider? posDataProvider = null,
        IAiSpeechAssistantDataProvider? aiSpeechAssistantDataProvider = null,
        IKnowledgeScenarioDataProvider? knowledgeScenarioDataProvider = null,
        ISalesDataProvider? salesDataProvider = null,
        IRedisSafeRunner? redisSafeRunner = null,
        ISmartTalkBackgroundJobClient? backgroundJobClient = null)
    {
        return new AiResourceSyncService(
            mediator ?? Substitute.For<IMediator>(),
            crmClient ?? Substitute.For<ICrmClient>(),
            agentDataProvider ?? Substitute.For<IAgentDataProvider>(),
            posDataProvider ?? Substitute.For<IPosDataProvider>(),
            aiSpeechAssistantDataProvider ?? Substitute.For<IAiSpeechAssistantDataProvider>(),
            knowledgeScenarioDataProvider ?? Substitute.For<IKnowledgeScenarioDataProvider>(),
            salesDataProvider ?? Substitute.For<ISalesDataProvider>(),
            Substitute.For<IWeChatClient>(),
            redisSafeRunner ?? Substitute.For<IRedisSafeRunner>(),
            backgroundJobClient ?? Substitute.For<ISmartTalkBackgroundJobClient>(),
            new SalesSettingBuilder().Build(),
            new SalesAutoCreateSettingBuilder().Build());
    }

    private sealed class SalesSettingBuilder
    {
        public SalesSetting Build()
        {
            return new SalesSetting(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sales:ApiKey"] = "test-key",
                ["Sales:BaseUrl"] = "https://example.com",
                ["Sales:CompanyName"] = "OME"
            }).Build());
        }
    }

    private sealed class SalesAutoCreateSettingBuilder
    {
        public SalesAutoCreateSetting Build()
        {
            return new SalesAutoCreateSetting(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesAutoCreate:NotifyRobotUrl"] = "https://example.com/robot",
                ["SalesAutoCreate:DefaultAssistantGreetings"] = "Hello",
                ["SalesAutoCreate:ServiceProviderId"] = "123"
            }).Build());
        }
    }
}
