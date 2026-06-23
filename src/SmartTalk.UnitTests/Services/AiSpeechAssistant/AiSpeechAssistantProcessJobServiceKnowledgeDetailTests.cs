using AutoMapper;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistant;

public class AiSpeechAssistantProcessJobServiceKnowledgeDetailTests
{
    [Fact]
    public async Task SyncAiSpeechAssistantKnowledgeDetailAsync_RefreshesOmeCompanyKnowledgeDetails()
    {
        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync("OME", Arg.Any<CancellationToken>())
            .Returns(new Company { Id = 1, Name = "OME" });

        var knowledgePromptService = Substitute.For<IAiSpeechAssistantKnowledgePromptService>();

        var sut = CreateSut(posDataProvider, knowledgePromptService);

        await sut.SyncAiSpeechAssistantKnowledgeDetailAsync(new SyncAiSpeechAssistantKnowledgeDetailCommand(), CancellationToken.None);

        await knowledgePromptService.Received(1)
            .RefreshKnowledgeDetailsByCompanyIdAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAiSpeechAssistantKnowledgeDetailAsync_SkipsWhenSalesCompanyMissing()
    {
        var posDataProvider = Substitute.For<IPosDataProvider>();
        posDataProvider.GetPosCompanyByNameAsync("OME", Arg.Any<CancellationToken>())
            .Returns((Company)null);

        var knowledgePromptService = Substitute.For<IAiSpeechAssistantKnowledgePromptService>();

        var sut = CreateSut(posDataProvider, knowledgePromptService);

        await sut.SyncAiSpeechAssistantKnowledgeDetailAsync(new SyncAiSpeechAssistantKnowledgeDetailCommand(), CancellationToken.None);

        await knowledgePromptService.DidNotReceive()
            .RefreshKnowledgeDetailsByCompanyIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private static AiSpeechAssistantProcessJobService CreateSut(
        IPosDataProvider posDataProvider,
        IAiSpeechAssistantKnowledgePromptService knowledgePromptService)
    {
        return new AiSpeechAssistantProcessJobService(
            Substitute.For<IMapper>(),
            Substitute.For<IVectorDb>(),
            Substitute.For<ICrmClient>(),
            new SalesSettingBuilder().Build(),
            Substitute.For<ITwilioService>(),
            posDataProvider,
            Substitute.For<TranslationClient>(),
            Substitute.For<IAgentDataProvider>(),
            Substitute.For<IRestaurantDataProvider>(),
            Substitute.For<IPhoneOrderDataProvider>(),
            Substitute.For<IAiSpeechAssistantDataProvider>(),
            Substitute.For<IFileTextExtractor>(),
            knowledgePromptService);
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
