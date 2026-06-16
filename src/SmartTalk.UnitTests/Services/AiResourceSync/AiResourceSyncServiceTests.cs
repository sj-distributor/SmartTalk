using System.Linq.Expressions;
using Mediator.Net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.AiSpeechAssistant;
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
using Xunit;

namespace SmartTalk.UnitTests.Services.AiResourceSync;

public class AiResourceSyncServiceTests
{
    [Fact]
    public async Task SyncCrmSalesAutoCreateAsync_ReturnsCustomerCount_AndEnqueuesBackgroundExecution()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<CrmSalesAutoSyncCustomerDto>
            {
                new() { CustomerId = "100" },
                new() { CustomerId = "200" }
            });

        var backgroundJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();
        Expression<Func<IAiResourceSyncProcessJobService, Task>>? queuedExpression = null;
        backgroundJobClient
            .Enqueue<IAiResourceSyncProcessJobService>(Arg.Do<Expression<Func<IAiResourceSyncProcessJobService, Task>>>(x => queuedExpression = x), Arg.Any<string>())
            .Returns("job-1");

        var sut = CreateSut(crmClient: crmClient, backgroundJobClient: backgroundJobClient);
        var command = new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123
        };

        var response = await sut.SyncCrmSalesAutoCreateAsync(command, CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.TotalCount);
        backgroundJobClient.Received(1).Enqueue(Arg.Any<Expression<Func<IAiResourceSyncProcessJobService, Task>>>(), Arg.Any<string>());
        Assert.NotNull(queuedExpression);

        var methodCall = Assert.IsAssignableFrom<MethodCallExpression>(queuedExpression.Body);
        Assert.Equal(nameof(IAiResourceSyncProcessJobService.ExecuteSyncCrmSalesAutoCreateAsync), methodCall.Method.Name);
    }

    [Fact]
    public async Task ExecuteSyncCrmSalesAutoCreateAsync_CreatesAssistantWithKnowledgeDetailsFromSceneItems()
    {
        var crmClient = Substitute.For<ICrmClient>();
        crmClient.GetSalesAutoSyncCustomersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
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
                    Names = "{\"en\":{\"name\":\"Alice GroupA\"},\"cn\":{\"name\":\"Alice GroupA\"}}"
                }
            });
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
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(101, Arg.Any<CancellationToken>())
            .Returns(new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Id = 101,
                AgentId = 201,
                Name = "100 (English)"
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

        var sut = CreateSut(
            mediator: mediator,
            crmClient: crmClient,
            agentDataProvider: agentDataProvider,
            posDataProvider: posDataProvider,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            knowledgeScenarioDataProvider: knowledgeScenarioDataProvider,
            salesDataProvider: salesDataProvider);
        
        await sut.SyncInternalAsync(new AiResourceSyncCommand
        {
            IsManual = true,
            ServiceProviderId = 123
        }, new List<CrmSalesAutoSyncCustomerDto>(), CancellationToken.None);

        await mediator.Received(1).SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(
            Arg.Is<AddAiSpeechAssistantCommand>(x =>
                x.AssistantName == "100 (English)" &&
                x.Greetings == "Hello" &&
                x.Json == "{}" &&
                x.Details.Count == 1 &&
                x.Details[0].KnowledgeName == "Scene item example" &&
                x.Details[0].FormatType == AiSpeechAssistantKonwledgeFormatType.FAQ &&
                x.Details[0].Content == "scene item content" &&
                x.Details[0].FileName == "scene-item.txt"),
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
