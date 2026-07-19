using NSubstitute;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistant;

public class KnowledgeDetailSyncWithSceneMetadataTests
{
    [Fact]
    public async Task RefreshKnowledgeDetailsByCompanyId_ShouldMigrateLegacyCrmSceneDetailsToRelations()
    {
        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        var knowledgeScenarioDataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();

        var knowledge = new AiSpeechAssistantKnowledge
        {
            Id = 100,
            AssistantId = 10,
            ModelLanguage = "Chinese",
            Prompt = "base prompt",
            ScenePrompt = string.Empty,
            IsActive = true
        };

        var legacyDetail = new AiSpeechAssistantKnowledgeDetail
        {
            Id = 1,
            KnowledgeId = 100,
            KnowledgeName = "item explain",
            FormatType = AiSpeechAssistantKonwledgeFormatType.Text,
            Content = "Use {HiFood_商品_术语库需求}"
        };

        knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(companyId: 1, isActive: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneLanguageMapping>
            {
                new()
                {
                    CompanyId = 1,
                    SceneId = 49,
                    Language = AutoAddLanguage.Chinese,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            });

        aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<CrmAutoSyncAssistantLocationDto>
            {
                new()
                {
                    AssistantId = 10,
                    StoreId = 1,
                    AgentId = 2,
                    Name = "crm-assistant"
                }
            });

        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesByCompanyIdAsync(1, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeCopyRelatedInfoDto>
            {
                new()
                {
                    AssistantId = 10,
                    KnowledgeId = 100,
                    AssiatantName = "crm-assistant",
                    SourceSystem = AgentSourceSystem.AiResource
                }
            });

        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 100),
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantKnowledge> { knowledge });
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
                null,
                100,
                null,
                Arg.Any<CancellationToken>())
            .Returns(knowledge);

        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(
                10,
                null,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns((1, new List<AiSpeechAssistantKnowledge> { knowledge }));

        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsByKnowledgeIdsAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 100),
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantKnowledgeSceneRelation>
            {
                new()
                {
                    Id = 2,
                    KnowledgeId = 100,
                    SceneId = 48,
                    SourceType = AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync
                }
            });
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsAsync(100, Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantKnowledgeSceneRelation>
            {
                new()
                {
                    KnowledgeId = 100,
                    SceneId = 49
                }
            });
        aiSpeechAssistantDataProvider.GetKnowledgeScenesByIdsAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 49),
                Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeScene>
            {
                new()
                {
                    Id = 49,
                    Status = KnowledgeSceneStatus.Published,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            });

        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeDetailsByKnowledgeIdsAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 100),
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantKnowledgeDetail> { legacyDetail });

        knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 49),
                Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneItem>
            {
                new()
                {
                    Id = 186,
                    SceneId = 49,
                    Name = "item explain",
                    Type = KnowledgeSceneItemType.Text,
                    Content = "Use {HiFood_商品_术语库需求}"
                }
            });

        var sut = new AiSpeechAssistantKnowledgePromptService(aiSpeechAssistantDataProvider, knowledgeScenarioDataProvider);

        await sut.RefreshKnowledgeDetailsByCompanyIdAsync(1, CancellationToken.None);

        await aiSpeechAssistantDataProvider.Received(1)
            .DeleteAiSpeechAssistantKnowledgeDetailsAsync(
                Arg.Is<List<AiSpeechAssistantKnowledgeDetail>>(x => x.Count == 1 && x[0].Id == 1),
                true,
                Arg.Any<CancellationToken>());

        await aiSpeechAssistantDataProvider.Received(1)
            .AddAiSpeechAssistantKnowledgeSceneRelationsAsync(
                Arg.Is<List<AiSpeechAssistantKnowledgeSceneRelation>>(x =>
                    x.Count == 1 &&
                    x[0].KnowledgeId == 100 &&
                    x[0].SceneId == 49 &&
                    x[0].SourceType == AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync),
                true,
                Arg.Any<CancellationToken>());

        await aiSpeechAssistantDataProvider.Received(1)
            .DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(
                Arg.Is<List<AiSpeechAssistantKnowledgeSceneRelation>>(x =>
                    x.Count == 1 &&
                    x[0].Id == 2 &&
                    x[0].SceneId == 48 &&
                    x[0].SourceType == AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync),
                true,
                Arg.Any<CancellationToken>());

        await aiSpeechAssistantDataProvider.Received(1)
            .UpdateAiSpeechAssistantKnowledgesAsync(
                Arg.Is<List<AiSpeechAssistantKnowledge>>(x =>
                    x.Count == 1 &&
                    x[0].Id == 100 &&
                    x[0].ScenePrompt == "场景知识点：\nUse {HiFood_商品_术语库需求}"),
                true,
                Arg.Any<CancellationToken>());
    }
}
