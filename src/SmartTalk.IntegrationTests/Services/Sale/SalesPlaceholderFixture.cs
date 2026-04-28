using System.Reflection;
using AutoMapper;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Domain.AIAssistant;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.Sale;

public class SalesPlaceholderFixture
{
    [Fact]
    public async Task ShouldBuildUnifiedDeliveryProgressPrompt()
    {
        var mapper = Substitute.For<IMapper>();
        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();
        var salesDataProvider = Substitute.For<ISalesDataProvider>();
        var assistantService = new AiSpeechAssistantService(
            clock: null!,
            mapper: mapper,
            currentUser: null!,
            azureSetting: null!,
            cacheManager: null!,
            openaiClient: null!,
            ffmpegService: null!,
            openAiSettings: null!,
            smartiesClient: null!,
            posUtilService: null!,
            zhiPuAiSettings: null!,
            redisSafeRunner: null!,
            posDataProvider: null!,
            phoneOrderService: null!,
            agentDataProvider: null!,
            attachmentService: null!,
            salesDataProvider: salesDataProvider,
            speechMaticsService: null!,
            speechToTextService: null!,
            fileTextExtractor: null!,
            workWeChatKeySetting: null!,
            httpClientFactory: null!,
            restaurantDataProvider: null!,
            phoneOrderDataProvider: null!,
            inactivityTimerManager: null!,
            backgroundJobClient: null!,
            twilioService: null!,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            openaiWebSocket: null!);

        aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInboundRouteAsync("+15550001", "+15550002", Arg.Any<CancellationToken>())
            .Returns([]);
        aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync("+15550001", "+15550002", null, Arg.Any<CancellationToken>())
            .Returns((
                new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
                {
                    Id = 1,
                    Name = "1001/1002"
                },
                new AiSpeechAssistantKnowledge
                {
                    AssistantId = 1,
                    Prompt = "到货信息:\n#{delivery_progress}"
                },
                new AiSpeechAssistantUserProfile
                {
                    AssistantId = 1,
                    CallerNumber = "+15550001",
                    ProfileJson = string.Empty
                }));
        salesDataProvider
            .GetDeliveryProgressCacheBySoldToIdsAsync(
                Arg.Is<List<string>>(x => x.Count == 2 && x[0] == "1001" && x[1] == "1002"),
                Arg.Any<CancellationToken>())
            .Returns(
            [
                new AiSpeechAssistantKnowledgeVariableCache { Filter = "1001", CacheValue = "到货1001" },
                new AiSpeechAssistantKnowledgeVariableCache { Filter = "1002", CacheValue = "到货1002" }
            ]);

        var buildMethod = typeof(AiSpeechAssistantService).GetMethod(
            "BuildingAiSpeechAssistantKnowledgeBaseAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        buildMethod.ShouldNotBeNull();

        var task = (Task)buildMethod!.Invoke(
            assistantService,
            ["+15550001", "+15550002", null, null, null, CancellationToken.None])!;
        await task;

        var streamContextField = typeof(AiSpeechAssistantService).GetField(
            "_aiSpeechAssistantStreamContext",
            BindingFlags.Instance | BindingFlags.NonPublic);
        streamContextField.ShouldNotBeNull();

        var streamContext = (AiSpeechAssistantStreamContextDto)streamContextField!.GetValue(assistantService)!;
        streamContext.LastPrompt.ShouldContain("到货信息:\n到货1001");
        streamContext.LastPrompt.ShouldContain("到货1002");
        streamContext.LastPrompt.ShouldNotContain("#{delivery_progress}");
    }

    [Fact]
    public async Task ShouldRefreshDeliveryProgressCacheByIndividualSoldToId()
    {
        var crmClient = Substitute.For<ICrmClient>();
        var salesService = Substitute.For<ISalesService>();
        var salesDataProvider = Substitute.For<ISalesDataProvider>();
        var backgroundJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

        salesService.BuildCustomerItemsStringAsync(
                Arg.Is<List<string>>(x => x.Count == 1 && x[0] == "1001"),
                Arg.Any<CancellationToken>())
            .Returns("商品1001");
        salesService.BuildCustomerItemsStringAsync(
                Arg.Is<List<string>>(x => x.Count == 1 && x[0] == "1002"),
                Arg.Any<CancellationToken>())
            .Returns("商品1002");
        salesService.BuildCustomerDeliveryProgressStringAsync(
                Arg.Is<List<string>>(x => x.Count == 1 && x[0] == "1001"),
                Arg.Any<CancellationToken>())
            .Returns("到货1001");
        salesService.BuildCustomerDeliveryProgressStringAsync(
                Arg.Is<List<string>>(x => x.Count == 1 && x[0] == "1002"),
                Arg.Any<CancellationToken>())
            .Returns("到货1002");

        var sut = new SalesJobProcessJobService(
            crmClient,
            null!,
            salesService,
            salesDataProvider,
            backgroundJobClient,
            null!,
            null!);

        await sut.RefreshCustomerItemsCacheBySoldToIdAsync("1001/1002", CancellationToken.None);

        await salesDataProvider.Received(1).UpsertCustomerItemsCacheAsync("1001", "商品1001", false, Arg.Any<CancellationToken>());
        await salesDataProvider.Received(1).UpsertCustomerItemsCacheAsync("1002", "商品1002", false, Arg.Any<CancellationToken>());
        await salesDataProvider.Received(1).UpsertDeliveryProgressCacheAsync("1001", "到货1001", false, Arg.Any<CancellationToken>());
        await salesDataProvider.Received(1).UpsertDeliveryProgressCacheAsync("1002", "到货1002", true, Arg.Any<CancellationToken>());
        await salesDataProvider.DidNotReceive().UpsertDeliveryProgressCacheAsync("1001/1002", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
