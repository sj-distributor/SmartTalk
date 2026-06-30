using AutoMapper;
using NSubstitute;
using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;
using Xunit;

namespace SmartTalk.UnitTests.Services.KnowledgeScenario;

public class AutoAddLanguageCatalogTests
{
    [Fact]
    public void AutoAddLanguage_ContainsAllFixedPageLanguages()
    {
        var languages = Enum.GetValues<AutoAddLanguage>();
        
        Assert.Contains(AutoAddLanguage.Spanish, languages);
        Assert.Contains(AutoAddLanguage.Korean, languages);
        Assert.Contains(AutoAddLanguage.Vietnamese, languages);
        Assert.Contains(AutoAddLanguage.Thai, languages);
    }

    [Fact]
    public async Task SaveKnowledgeSceneLanguageMappings_DoesNotAutoCreateKnowledgeSceneCompanyAuthorization()
    {
        var dataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        dataProvider.GetKnowledgeSceneLanguageMappingsAsync(companyId: 1, isActive: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneLanguageMapping>());

        var sut = new KnowledgeScenarioService(
            Substitute.For<IMapper>(),
            dataProvider,
            Substitute.For<IAiSpeechAssistantDataProvider>(),
            Substitute.For<IPosDataProvider>(),
            Substitute.For<ISmartiesClient>(),
            Substitute.For<IAiSpeechAssistantKnowledgePromptService>(),
            new SalesSettingBuilder().Build());

        await sut.SaveKnowledgeSceneLanguageMappingsAsync(new SaveKnowledgeSceneLanguageMappingsCommand
        {
            CompanyId = 1,
            Mappings =
            [
                new SaveKnowledgeSceneLanguageMappingItemDto
                {
                    Language = AutoAddLanguage.English,
                    SceneId = 100
                }
            ]
        }, CancellationToken.None);

        await dataProvider.DidNotReceive()
            .AddKnowledgeSceneCompaniesAsync(Arg.Any<List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneCompany>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveKnowledgeSceneLanguageMappings_EmptyMappings_ShouldClearExistingMappings()
    {
        var existingMappings = new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneLanguageMapping>
        {
            new()
            {
                Id = 1,
                CompanyId = 1,
                SceneId = 100,
                Language = AutoAddLanguage.English,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = 2,
                CompanyId = 1,
                SceneId = 200,
                Language = AutoAddLanguage.Spanish,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var dataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        dataProvider.GetKnowledgeSceneLanguageMappingsAsync(companyId: 1, isActive: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(existingMappings, new List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneLanguageMapping>());

        var sut = new KnowledgeScenarioService(
            Substitute.For<IMapper>(),
            dataProvider,
            Substitute.For<IAiSpeechAssistantDataProvider>(),
            Substitute.For<IPosDataProvider>(),
            Substitute.For<ISmartiesClient>(),
            Substitute.For<IAiSpeechAssistantKnowledgePromptService>(),
            new SalesSettingBuilder().Build());

        var response = await sut.SaveKnowledgeSceneLanguageMappingsAsync(new SaveKnowledgeSceneLanguageMappingsCommand
        {
            CompanyId = 1,
            Mappings = []
        }, CancellationToken.None);

        Assert.All(existingMappings, x => Assert.False(x.IsActive));
        await dataProvider.Received(1)
            .UpdateKnowledgeSceneLanguageMappingsAsync(Arg.Is<List<SmartTalk.Core.Domain.KnowledgeScenario.KnowledgeSceneLanguageMapping>>(x => x.Count == 2), true, Arg.Any<CancellationToken>());
        Assert.NotNull(response.Data);
        Assert.NotNull(response.Data.Mappings);
        Assert.All(response.Data.Mappings, x => Assert.Null(x.SceneId));
    }

    [Fact]
    public async Task GetKnowledgeSceneLanguageMappings_ShouldHideDeletedScenes()
    {
        var dataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        dataProvider.GetKnowledgeSceneLanguageMappingsAsync(companyId: 1, isActive: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneLanguageMapping>
            {
                new()
                {
                    Id = 7,
                    CompanyId = 1,
                    SceneId = 54,
                    Language = AutoAddLanguage.Japanese,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            });
        dataProvider.GetKnowledgeScenesByIdsAsync(Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 54), Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeScene>());

        var sut = new KnowledgeScenarioService(
            Substitute.For<IMapper>(),
            dataProvider,
            Substitute.For<IAiSpeechAssistantDataProvider>(),
            Substitute.For<IPosDataProvider>(),
            Substitute.For<ISmartiesClient>(),
            Substitute.For<IAiSpeechAssistantKnowledgePromptService>(),
            new SalesSettingBuilder().Build());

        var response = await sut.GetKnowledgeSceneLanguageMappingsAsync(new GetKnowledgeSceneLanguageMappingsRequest
        {
            CompanyId = 1
        }, CancellationToken.None);

        var japanese = response.Data.Mappings.Single(x => x.Language == AutoAddLanguage.Japanese);
        Assert.Null(japanese.MappingId);
        Assert.Null(japanese.SceneId);
        Assert.Null(japanese.SceneName);
    }

    [Fact]
    public async Task DeleteKnowledgeScene_ShouldDeactivateLanguageMappingsForDeletedScene()
    {
        var scene = new KnowledgeScene
        {
            Id = 54,
            Name = "scene-54"
        };
        var languageMappings = new List<KnowledgeSceneLanguageMapping>
        {
            new()
            {
                Id = 9,
                CompanyId = 1,
                SceneId = 54,
                Language = AutoAddLanguage.Japanese,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var dataProvider = Substitute.For<IKnowledgeScenarioDataProvider>();
        dataProvider.GetKnowledgeScenesByIdsAsync(Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 54), Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeScene> { scene });
        dataProvider.GetKnowledgeSceneLanguageMappingsAsync(sceneIds: Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 54), isActive: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(languageMappings);
        dataProvider.GetKnowledgeSceneItemsBySceneIdAsync(54, Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneItem>());
        dataProvider.GetKnowledgeSceneHistoriesAsync(sceneId: 54, historyId: null, pageIndex: null, pageSize: null, cancellationToken: Arg.Any<CancellationToken>())
            .Returns((0, new List<KnowledgeSceneHistory>()));
        dataProvider.GetKnowledgeSceneHistoryItemsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneHistoryItem>());
        dataProvider.GetKnowledgeSceneCompaniesBySceneIdsAsync(
                Arg.Is<List<int>>(x => x.Count == 1 && x[0] == 54),
                companyId: null,
                storeId: null,
                isApplied: null,
                isCompanyAuthorization: null,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSceneCompany>());

        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(54, Arg.Any<CancellationToken>())
            .Returns(new List<AiSpeechAssistantKnowledgeSceneRelation>());

        var mapper = Substitute.For<IMapper>();
        mapper.Map<KnowledgeSceneDto>(Arg.Any<KnowledgeScene>())
            .Returns(callInfo =>
            {
                var source = callInfo.Arg<KnowledgeScene>();
                return new KnowledgeSceneDto
                {
                    Id = source.Id,
                    Name = source.Name,
                    SceneItems = new List<KnowledgeSceneItemDto>()
                };
            });
        mapper.Map<List<KnowledgeSceneItemDto>>(Arg.Any<List<KnowledgeSceneItem>>())
            .Returns(new List<KnowledgeSceneItemDto>());

        var sut = new KnowledgeScenarioService(
            mapper,
            dataProvider,
            aiSpeechAssistantDataProvider,
            Substitute.For<IPosDataProvider>(),
            Substitute.For<ISmartiesClient>(),
            Substitute.For<IAiSpeechAssistantKnowledgePromptService>(),
            new SalesSettingBuilder().Build());

        await sut.DeleteKnowledgeSceneAsync(new DeleteKnowledgeSceneCommand
        {
            Id = 54
        }, CancellationToken.None);

        Assert.False(languageMappings[0].IsActive);
        await dataProvider.Received(1).UpdateKnowledgeSceneLanguageMappingsAsync(
            Arg.Is<List<KnowledgeSceneLanguageMapping>>(x => x.Count == 1 && x[0].SceneId == 54 && !x[0].IsActive),
            false,
            Arg.Any<CancellationToken>());
    }

    private sealed class SalesSettingBuilder
    {
        public SalesSetting Build()
        {
            return new SalesSetting(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sales:CompanyName"] = "OME"
            }).Build());
        }
    }
}
