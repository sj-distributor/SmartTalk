using Mediator.Net;
using Mediator.Net.Context;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Handlers.CommandHandlers.AiResourceSync;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.AiResourceSync;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiResourceSync;

public class AiResourceSyncServiceTests
{
    [Fact]
    public async Task AiResourceSyncCommandHandler_ExecutesBackgroundSync()
    {
        var processJobService = Substitute.For<IAiResourceSyncProcessJobService>();
        var handler = new AiResourceSyncCommandHandler(processJobService);
        var context = Substitute.For<IReceiveContext<AiResourceSyncCommand>>();
        context.Message.Returns(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        });

        var response = await handler.Handle(context, CancellationToken.None);

        await processJobService.Received(1).ExecuteSyncCrmSalesAutoCreateAsync(
            Arg.Is<AiResourceSyncCommand>(x => x.IsManual && x.ServiceProviderId == 123 && x.InitiatedByUserId == 888),
            Arg.Any<CancellationToken>());
        Assert.NotNull(response);
    }

    [Fact]
    public async Task SchedulingRefreshCrmCustomerContactPhoneMapCommandHandler_RefreshesContactPhoneMaps()
    {
        var processJobService = Substitute.For<IAiResourceSyncProcessJobService>();
        var handler = new SchedulingRefreshCrmCustomerContactPhoneMapCommandHandler(processJobService);
        var context = Substitute.For<IReceiveContext<SchedulingRefreshCrmCustomerContactPhoneMapCommand>>();
        context.Message.Returns(new SchedulingRefreshCrmCustomerContactPhoneMapCommand());

        await handler.Handle(context, CancellationToken.None);

        await processJobService.Received(1).RefreshCrmCustomerContactPhoneMapsAsync(
            Arg.Any<SchedulingRefreshCrmCustomerContactPhoneMapCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteSyncCrmSalesAutoCreateAsync_CreatesAssistantWithKnowledgeDetailsFromSceneItems()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(
                Arg.Any<int>(),
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
        crmClient.GetChangedSalesAutoSyncCustomersAsync(
                Arg.Any<CancellationToken>())
            .Returns(new List<CrmSalesAutoSyncCustomerDto>
            {
                new()
                {
                    CustomerId = "100",
                    SalesName = "Alice",
                    SalesGroup = "GroupA",
                    Language = "English"
                }
            });

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
        AddAiSpeechAssistantCommand? capturedAddAssistantCommand = null;
        mediator.When(x => x.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
                Arg.Any<AddAiSpeechAssistantCommand>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => capturedAddAssistantCommand = callInfo.Arg<AddAiSpeechAssistantCommand>());

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
                    SourceSystem = AgentSourceSystem.AiResource,
                    CreatedDate = DateTimeOffset.UtcNow
                }
            });
        agentDataProvider.GetAgentByIdAsync(201, Arg.Any<CancellationToken>())
            .Returns(new Agent
            {
                Id = 201,
                Name = "Alice GroupA",
                Type = AgentType.Sales,
                SourceSystem = AgentSourceSystem.AiResource,
                CreatedDate = DateTimeOffset.UtcNow
            });

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<CrmAutoSyncAssistantLocationDto>
            {
                new() { AssistantId = 999, StoreId = 9, AgentId = 99, Name = "200" },
                new() { AssistantId = 999, StoreId = 9, AgentId = 99, Name = "200" }
            });
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
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(101, null, true, Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistantKnowledge
            {
                Id = 701,
                AssistantId = 101,
                IsActive = true,
                ModelLanguage = "English"
            });
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsAsync(701, Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistantKnowledgeSceneRelation>
            {
                new()
                {
                    Id = 1,
                    KnowledgeId = 701,
                    SceneId = 499,
                    SourceType = AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync
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
        knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
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
        }, CancellationToken.None);

        await mediator.Received(1).SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
            Arg.Any<AddAiSpeechAssistantCommand>(),
            Arg.Any<CancellationToken>());
        Assert.NotNull(capturedAddAssistantCommand);
        Assert.Equal("100", capturedAddAssistantCommand!.AssistantName);
        Assert.Equal(888, capturedAddAssistantCommand.CreatedBy);
        Assert.Empty(capturedAddAssistantCommand.Details);
        await aiSpeechAssistantDataProvider.Received(1).AddAiSpeechAssistantKnowledgeSceneRelationsAsync(
            Arg.Is<List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistantKnowledgeSceneRelation>>(x =>
                x.Count == 1 &&
                x[0].KnowledgeId == 701 &&
                x[0].SceneId == 500 &&
                x[0].SourceType == AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync),
            true,
            Arg.Any<CancellationToken>());
        await aiSpeechAssistantDataProvider.Received(1).DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(
            Arg.Is<List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistantKnowledgeSceneRelation>>(x =>
                x.Count == 1 &&
                x[0].KnowledgeId == 701 &&
                x[0].SceneId == 499 &&
                x[0].SourceType == AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync),
            true,
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
        crmClient.GetChangedSalesAutoSyncCustomersAsync(
                Arg.Any<CancellationToken>())
            .Returns(new List<CrmSalesAutoSyncCustomerDto>
            {
                new()
                {
                    CustomerId = "100",
                    SalesName = "Alice",
                    SalesGroup = "GroupA",
                    Language = "English"
                }
            });

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
        }, CancellationToken.None));

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
        crmClient.GetChangedSalesAutoSyncCustomersAsync(
                Arg.Any<CancellationToken>())
            .Returns(new List<CrmSalesAutoSyncCustomerDto>
            {
                new()
                {
                    CustomerId = "116399",
                    SalesName = "CHRISTINE.C",
                    SalesGroup = "057",
                    Language = "English"
                }
            });

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
                    SourceSystem = AgentSourceSystem.AiResource,
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
        knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
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
        }, CancellationToken.None);

        await mediator.DidNotReceive().SendAsync<AddAgentCommand, AddAgentResponse>(
            Arg.Any<AddAgentCommand>(),
            Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
            Arg.Any<AddAiSpeechAssistantCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncInternalAsync_ManualSync_UsesChangedCustomersEndpoint()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetChangedSalesAutoSyncCustomersAsync(
                Arg.Any<CancellationToken>())
            .Returns(new List<CrmSalesAutoSyncCustomerDto>());
        crmClient.GetSalesAutoSyncCustomersAsync(
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns((new List<CrmSalesAutoSyncCustomerDto>(), 0));

        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        var sut = CreateSut(
            crmClient: crmClient,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider);

        await sut.SyncInternalAsync(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        }, CancellationToken.None);

        await crmClient.Received(1).GetChangedSalesAutoSyncCustomersAsync(
            Arg.Any<CancellationToken>());
        await crmClient.DidNotReceive().GetSalesAutoSyncCustomersAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncInternalAsync_WhenCustomerSalesChanges_ShouldMoveAssistantToTargetSalesAgentWithoutMovingSourceAgent()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetChangedSalesAutoSyncCustomersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CrmSalesAutoSyncCustomerDto>
            {
                new()
                {
                    CustomerId = "1",
                    SalesName = "B",
                    SalesGroup = "G2",
                    Language = "English"
                }
            });

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
                    Names = "{\"en\":{\"name\":\"A G1\"}}",
                    CreatedDate = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new()
                {
                    Id = 20,
                    CompanyId = 1,
                    Names = "{\"en\":{\"name\":\"B G2\"}}",
                    CreatedDate = DateTimeOffset.UtcNow
                }
            });
        posDataProvider.GetPosAgentsAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 20),
                null,
                Arg.Any<CancellationToken>())
            .Returns(new List<PosAgent> { new() { StoreId = 20, AgentId = 201 } });

        var agentDataProvider = Substitute.For<IAgentDataProvider>();
        agentDataProvider.GetAgentsByIdsAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 201),
                Arg.Any<CancellationToken>())
            .Returns(new List<Agent>
            {
                new()
                {
                    Id = 201,
                    Name = "B G2",
                    Type = AgentType.Sales,
                    SourceSystem = AgentSourceSystem.AiResource
                }
            });

        var assistant = new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 900,
            AgentId = 300,
            Name = "1"
        };
        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<CrmAutoSyncAssistantLocationDto>
            {
                new() { AssistantId = 900, StoreId = 10, AgentId = 300, Name = "1" }
            });
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(900, Arg.Any<CancellationToken>())
            .Returns(assistant);
        aiSpeechAssistantDataProvider.GetAgentAssistantsAsync(
                Arg.Any<List<int>>(),
                Arg.Any<List<int>>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.AISpeechAssistant.AgentAssistant>
            {
                new() { Id = 1, AgentId = 300, AssistantId = 900 }
            });
        aiSpeechAssistantDataProvider.DeleteAgentAssistantsAsync(
                Arg.Any<List<SmartTalk.Core.Domain.AISpeechAssistant.AgentAssistant>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        aiSpeechAssistantDataProvider.AddAgentAssistantsAsync(
                Arg.Any<List<SmartTalk.Core.Domain.AISpeechAssistant.AgentAssistant>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync(
                Arg.Any<List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var knowledgeScenarioDataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(1, null, null, true, Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneLanguageMapping>
            {
                new() { CompanyId = 1, SceneId = 500, Language = AutoAddLanguage.English, CreatedAt = DateTimeOffset.UtcNow }
            });
        knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeScene> { new() { Id = 500, Status = KnowledgeSceneStatus.Published } });
        knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneItem>());

        var sut = CreateSut(
            crmClient: crmClient,
            agentDataProvider: agentDataProvider,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            knowledgeScenarioDataProvider: knowledgeScenarioDataProvider);

        await sut.SyncInternalAsync(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        }, CancellationToken.None);

        await aiSpeechAssistantDataProvider.Received(1).DeleteAgentAssistantsAsync(
            Arg.Is<List<SmartTalk.Core.Domain.AISpeechAssistant.AgentAssistant>>(x =>
                x.Count == 1 && x[0].AgentId == 300 && x[0].AssistantId == 900),
            true,
            Arg.Any<CancellationToken>());
        await aiSpeechAssistantDataProvider.Received(1).AddAgentAssistantsAsync(
            Arg.Is<List<SmartTalk.Core.Domain.AISpeechAssistant.AgentAssistant>>(x =>
                x.Count == 1 && x[0].AgentId == 201 && x[0].AssistantId == 900),
            true,
            Arg.Any<CancellationToken>());
        await aiSpeechAssistantDataProvider.Received(1).UpdateAiSpeechAssistantsAsync(
            Arg.Is<List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(x =>
                x.Count == 1 && x[0].Id == 900 && x[0].AgentId == 201),
            true,
            Arg.Any<CancellationToken>());
        await posDataProvider.DidNotReceive().UpdatePosAgentsAsync(
            Arg.Any<List<PosAgent>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCrmCustomerContactPhoneMapsAsync_RefreshesMultipleContactPhoneMappingsForSameCustomer()
    {
        var customer = new CrmSalesAutoSyncCustomerDto
        {
            CustomerId = "118895",
            CustomerName = "PHO DAY",
            SalesName = "TIFFANY.X",
            SalesGroup = "008",
            Language = "中文",
            Contacts =
            [
                new() { Name = "NICOLE", Phone = "415-218-2467", Identity = "老闆", Language = "粵語" },
                new() { Name = "JINGXIAN", Phone = "415-535-7933", Identity = "未知", Language = "粵語" },
                new() { Name = "STEVEN", Phone = "415-407-6788", Identity = "未知", Language = "粵語" }
            ]
        };

        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(([customer], 1));

        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.Pos.Company { Id = 1, Name = "OME" });
        posDataProvider.GetPosCompanyStoresAsync(
                Arg.Any<List<int>>(), Arg.Any<List<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new SmartTalk.Core.Domain.Pos.CompanyStore
                {
                    Id = 10,
                    CompanyId = 1,
                    CreatedDate = DateTimeOffset.UtcNow,
                    Names = "{\"en\":{\"name\":\"TIFFANY.X 008\"},\"cn\":{\"name\":\"TIFFANY.X 008\"}}"
                }
            ]);
        posDataProvider.GetPosAgentsAsync(Arg.Any<List<int>>(), null, Arg.Any<CancellationToken>())
            .Returns(
            [
                new PosAgent { StoreId = 10, AgentId = 201, CreatedDate = DateTimeOffset.UtcNow }
            ]);

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(10, "118895", Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Id = 9801,
                AgentId = 201,
                Name = "118895"
            });
        aiSpeechAssistantDataProvider.GetCrmCustomerContactPhoneMapsByCompanyIdAsync(1, Arg.Any<CancellationToken>())
            .Returns([]);

        List<SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap> capturedMappings = null;
        aiSpeechAssistantDataProvider
            .When(x => x.AddCrmCustomerContactPhoneMapsAsync(Arg.Any<List<SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap>>(), true, Arg.Any<CancellationToken>()))
            .Do(call => capturedMappings = call.Arg<List<SmartTalk.Core.Domain.Sales.CrmCustomerContactPhoneMap>>());

        var sut = CreateSut(
            crmClient: crmClient,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider);

        await sut.RefreshCrmCustomerContactPhoneMapsAsync(CancellationToken.None);

        Assert.NotNull(capturedMappings);
        Assert.Equal(3, capturedMappings.Count);
        Assert.All(capturedMappings, x =>
        {
            Assert.Equal("118895", x.CustomerId);
            Assert.Equal("PHO DAY", x.CustomerName);
            Assert.Equal(9801, x.AssistantId);
            Assert.Equal(201, x.AgentId);
            Assert.True(x.IsActive);
        });
        Assert.Contains(capturedMappings, x => x.ContactPhoneNormalized == "4152182467" && x.ContactName == "NICOLE");
        Assert.Contains(capturedMappings, x => x.ContactPhoneNormalized == "4155357933" && x.ContactName == "JINGXIAN");
        Assert.Contains(capturedMappings, x => x.ContactPhoneNormalized == "4154076788" && x.ContactName == "STEVEN");
    }
    [Fact]
    public async Task SyncInternalAsync_ManualSync_SplitsExistingAssistantWhenOnlyChangedCustomerIsReturned()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetChangedSalesAutoSyncCustomersAsync(
                Arg.Any<CancellationToken>())
            .Returns(new List<CrmSalesAutoSyncCustomerDto>
            {
                new()
                {
                    CustomerId = "1",
                    SalesName = "B",
                    SalesGroup = "G2",
                    Language = "English"
                }
            });
        crmClient.GetSalesAutoSyncCustomersAsync(
                Arg.Any<int>(),
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
        crmClient.GetSalesAutoSyncCustomerBySapIdAsync("1", Arg.Any<CancellationToken>())
            .Returns(new CrmSalesAutoSyncCustomerDto
            {
                CustomerId = "1",
                SalesName = "B",
                SalesGroup = "G2",
                Language = "English"
            });
        crmClient.GetSalesAutoSyncCustomerBySapIdAsync("2", Arg.Any<CancellationToken>())
            .Returns(new CrmSalesAutoSyncCustomerDto
            {
                CustomerId = "2",
                SalesName = "A",
                SalesGroup = "G1",
                Language = "English"
            });
        crmClient.GetSalesAutoSyncCustomersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((
                new List<CrmSalesAutoSyncCustomerDto>
                {
                    new()
                    {
                        CustomerId = "1",
                        SalesName = "B",
                        SalesGroup = "G2",
                        Language = "English"
                    },
                    new()
                    {
                        CustomerId = "2",
                        SalesName = "A",
                        SalesGroup = "G1",
                        Language = "English"
                    }
                },
                2
            ));

        var mediator = Substitute.For<IMediator>();
        mediator.SendAsync<AddAgentCommand, AddAgentResponse>(
                Arg.Any<AddAgentCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AddAgentResponse { Data = new AgentDto { Id = 201 } });
        mediator.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
                Arg.Any<AddAiSpeechAssistantCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AddAiSpeechAssistantResponse { Data = new AiSpeechAssistantDto { Id = 901 } });

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
                    CreatedDate = DateTimeOffset.UtcNow.AddDays(-1),
                    Names = "{\"en\":{\"name\":\"A G1\"},\"cn\":{\"name\":\"A G1\"}}"
                },
                new()
                {
                    Id = 20,
                    CompanyId = 1,
                    CreatedDate = DateTimeOffset.UtcNow,
                    Names = "{\"en\":{\"name\":\"B G2\"},\"cn\":{\"name\":\"B G2\"}}"
                }
            });
        posDataProvider.GetPosAgentsAsync(Arg.Any<List<int>>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<PosAgent>());

        var agentDataProvider = Substitute.For<IAgentDataProvider>();
        agentDataProvider.GetAgentsByIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Agent>());
        agentDataProvider.GetCrmAutoSyncAgentByStoreAndNameAsync(20, "B G2", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Agent>(null), Task.FromResult(new Agent
            {
                Id = 201,
                Name = "B G2",
                Type = AgentType.Sales,
                SourceSystem = AgentSourceSystem.AiResource,
                CreatedDate = DateTimeOffset.UtcNow
            }));
        agentDataProvider.GetAgentByIdAsync(201, Arg.Any<CancellationToken>())
            .Returns(new Agent
            {
                Id = 201,
                Name = "B G2",
                Type = AgentType.Sales,
                SourceSystem = AgentSourceSystem.AiResource,
                CreatedDate = DateTimeOffset.UtcNow
            });

        var oldAssistant = new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 900,
            AgentId = 300,
            Name = "1/2",
            CreatedDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var newAssistant = new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 901,
            AgentId = 201,
            Name = "1",
            CreatedDate = DateTimeOffset.UtcNow
        };

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.HasCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<CrmAutoSyncAssistantLocationDto>
            {
                new()
                {
                    AssistantId = 900,
                    StoreId = 10,
                    AgentId = 300,
                    Name = "1/2"
                }
            });
        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantByStoreAndNameAsync(20, "1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>(null), Task.FromResult(newAssistant));
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(900, Arg.Any<CancellationToken>()).Returns(oldAssistant);
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(901, Arg.Any<CancellationToken>()).Returns(newAssistant);
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
        knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
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

        var sut = CreateSut(
            mediator: mediator,
            crmClient: crmClient,
            agentDataProvider: agentDataProvider,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            knowledgeScenarioDataProvider: knowledgeScenarioDataProvider,
            salesDataProvider: salesDataProvider);

        var result = await sut.SyncInternalAsync(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123,
            InitiatedByUserId = 888
        }, CancellationToken.None);

        await mediator.Received(1).SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
            Arg.Is<AddAiSpeechAssistantCommand>(x => x.AssistantName == "1"),
            Arg.Any<CancellationToken>());
        await aiSpeechAssistantDataProvider.Received().UpdateAiSpeechAssistantsAsync(
            Arg.Is<List<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>(x => x.Any(a => a.Id == 900 && a.Name == "2")),
            true,
            Arg.Any<CancellationToken>());
        Assert.Contains(result.Stats.RenamedAssistants, x => x.AssistantId == 900 && x.AssistantName == "2");
    }

    private static AiResourceSyncService CreateSut(
        IMediator? mediator = null,
        ICrmClient? crmClient = null,
        IAgentDataProvider? agentDataProvider = null,
        IPosDataProvider? posDataProvider = null,
        IAiSpeechAssistantDataProvider? aiSpeechAssistantDataProvider = null,
        IAiSpeechAssistantKnowledgePromptService? aiSpeechAssistantKnowledgePromptService = null,
        IKnowledgeScenarioDataProvider? knowledgeScenarioDataProvider = null,
        ISalesDataProvider? salesDataProvider = null,
        IRedisSafeRunner? redisSafeRunner = null,
        ISmartTalkBackgroundJobClient? backgroundJobClient = null)
    {
        var redisRunner = redisSafeRunner ?? Substitute.For<IRedisSafeRunner>();
        redisRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<AiResourceSyncService.StoreLockResult>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<AiResourceSyncService.StoreLockResult>>>()());
        redisRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<Agent>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<Agent>>>()());
        redisRunner.ExecuteWithLockAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<SmartTalk.Messages.Enums.Caching.RedisServer>())
            .Returns(callInfo => callInfo.Arg<Func<Task<SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant>>>()());

        return new AiResourceSyncService(
            mediator ?? Substitute.For<IMediator>(),
            crmClient ?? Substitute.For<ICrmClient>(),
            agentDataProvider ?? Substitute.For<IAgentDataProvider>(),
            posDataProvider ?? Substitute.For<IPosDataProvider>(),
            aiSpeechAssistantDataProvider ?? Substitute.For<IAiSpeechAssistantDataProvider>(),
            aiSpeechAssistantKnowledgePromptService ?? Substitute.For<IAiSpeechAssistantKnowledgePromptService>(),
            knowledgeScenarioDataProvider ?? Substitute.For<IKnowledgeScenarioDataProvider>(),
            salesDataProvider ?? Substitute.For<ISalesDataProvider>(),
            Substitute.For<IWeChatClient>(),
            redisRunner,
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
        public AiResourceSyncSetting Build()
        {
            return new AiResourceSyncSetting(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesAutoCreate:NotifyRobotUrl"] = "https://example.com/robot",
                ["SalesAutoCreate:DefaultAssistantGreetings"] = "Hello",
                ["SalesAutoCreate:ServiceProviderId"] = "123"
            }).Build());
        }
    }
}
