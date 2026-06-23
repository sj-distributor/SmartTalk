using AutoMapper;
using NSubstitute;
using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
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
