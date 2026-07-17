using NSubstitute;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistant;

public class KnowledgeDetailSyncWithSceneMetadataTests
{
    [Fact]
    public async Task RefreshKnowledgeDetailsByCompanyId_ShouldRebuildLegacyCrmDetailsWithSceneMetadata()
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
            KnowledgeName = "EN",
            FormatType = AiSpeechAssistantKonwledgeFormatType.Text,
            Content = "old content"
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
                    AssiatantName = "crm-assistant"
                }
            });

        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 100),
                Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantKnowledge> { knowledge });

        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(
                10,
                null,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns((1, new List<AiSpeechAssistantKnowledge> { knowledge }));

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
            .AddAiSpeechAssistantKnowledgeDetailsAsync(
                Arg.Is<List<AiSpeechAssistantKnowledgeDetail>>(x =>
                    x.Count == 1 &&
                    x[0].KnowledgeId == 100 &&
                    x[0].KnowledgeName == "item explain" &&
                    x[0].Content == "Use {HiFood_商品_术语库需求}" &&
                    x[0].SourceType == "scene" &&
                    x[0].SourceSceneId == 49 &&
                    x[0].SourceSceneItemId == 186),
                true,
                Arg.Any<CancellationToken>());

        await aiSpeechAssistantDataProvider.Received(1)
            .UpdateAiSpeechAssistantKnowledgesAsync(
                Arg.Is<List<AiSpeechAssistantKnowledge>>(x =>
                    x.Count == 1 &&
                    x[0].Id == 100 &&
                    x[0].Prompt == "场景知识点：\nUse {HiFood_商品_术语库需求}" &&
                    x[0].ScenePrompt == string.Empty),
                true,
                Arg.Any<CancellationToken>());
    }
}
