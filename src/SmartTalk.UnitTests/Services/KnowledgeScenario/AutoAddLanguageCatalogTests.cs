using AutoMapper;
using NSubstitute;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
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
            Substitute.For<IAiSpeechAssistantKnowledgePromptService>());

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
}
