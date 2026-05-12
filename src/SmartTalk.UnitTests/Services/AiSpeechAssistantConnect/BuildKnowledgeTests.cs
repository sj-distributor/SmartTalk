using System.Reflection;
using AutoMapper;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Domain.AIAssistant;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

public class BuildKnowledgeTests
{
    [Fact]
    public async Task BuildKnowledgeAsync_ShouldReplaceDeliveryProgress()
    {
        var mapper = Substitute.For<IMapper>();
        var salesDataProvider = Substitute.For<ISalesDataProvider>();
        var aiSpeechAssistantDataProvider = Substitute.For<IAiSpeechAssistantDataProvider>();

        var assistant = new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Id = 1,
            Name = "1001/1002"
        };
        var knowledge = new AiSpeechAssistantKnowledge
        {
            AssistantId = 1,
            Prompt = "到货信息:\n#{delivery_progress}"
        };

        aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync("+15550001", "+15550002", null, Arg.Any<CancellationToken>())
            .Returns((assistant, knowledge, (AiSpeechAssistantUserProfile)null!));

        mapper.Map<AiSpeechAssistantDto>(assistant).Returns(new AiSpeechAssistantDto
        {
            Id = assistant.Id,
            Name = assistant.Name
        });
        mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge).Returns(new AiSpeechAssistantKnowledgeDto
        {
            AssistantId = knowledge.AssistantId,
            Prompt = knowledge.Prompt
        });

        salesDataProvider
            .GetDeliveryProgressCacheBySoldToIdsAsync(
                Arg.Is<List<string>>(x => x.Count == 2 && x[0] == "1001" && x[1] == "1002"),
                Arg.Any<CancellationToken>())
            .Returns(
            [
                new AiSpeechAssistantKnowledgeVariableCache { Filter = "1001", CacheValue = "到货1001" },
                new AiSpeechAssistantKnowledgeVariableCache { Filter = "1002", CacheValue = "到货1002" }
            ]);

        var sut = new AiSpeechAssistantConnectService(
            clock: null!,
            mapper: mapper,
            posDataProvider: null!,
            salesDataProvider: salesDataProvider,
            agentDataProvider: null!,
            aiSpeechAssistantDataProvider: aiSpeechAssistantDataProvider,
            ffmpegService: null!,
            posUtilService: null!,
            realtimeAiService: null!,
            openaiClient: null!,
            smartiesClient: null!,
            backgroundJobClient: null!);

        SetContext(sut, new AiSpeechAssistantConnectContext
        {
            From = "+15550001",
            To = "+15550002"
        });

        var buildKnowledgeMethod = typeof(AiSpeechAssistantConnectService).GetMethod(
            "BuildKnowledgeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        buildKnowledgeMethod.ShouldNotBeNull();

        var task = (Task)buildKnowledgeMethod!.Invoke(sut, [CancellationToken.None])!;
        await task.ConfigureAwait(false);

        var context = GetContext(sut);
        context.Prompt.ShouldContain("到货信息:\n到货1001");
        context.Prompt.ShouldContain("到货1002");
        context.Prompt.ShouldNotContain("#{delivery_progress}");
    }

    private static AiSpeechAssistantConnectContext GetContext(AiSpeechAssistantConnectService sut)
    {
        var contextField = typeof(AiSpeechAssistantConnectService).GetField(
            "_ctx",
            BindingFlags.Instance | BindingFlags.NonPublic);
        contextField.ShouldNotBeNull();

        return (AiSpeechAssistantConnectContext)contextField!.GetValue(sut)!;
    }

    private static void SetContext(AiSpeechAssistantConnectService sut, AiSpeechAssistantConnectContext context)
    {
        var contextField = typeof(AiSpeechAssistantConnectService).GetField(
            "_ctx",
            BindingFlags.Instance | BindingFlags.NonPublic);
        contextField.ShouldNotBeNull();

        contextField!.SetValue(sut, context);
    }
}
