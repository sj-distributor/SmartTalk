using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldGetAssistantInfo_WithKnowledgeAndUserProfile()
    {
        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "InfoAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            await repository.InsertAsync(new AiSpeechAssistantKnowledge
            {
                AssistantId = assistant.Id, Prompt = "Test prompt", IsActive = true, Version = "1.0"
            });

            await repository.InsertAsync(new Core.Domain.AIAssistant.AiSpeechAssistantUserProfile
            {
                AssistantId = assistant.Id, CallerNumber = TestCallerNumber, ProfileJson = "{\"name\":\"TestUser\"}"
            });
        });

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var (assistant, knowledge, userProfile) =
                await dataProvider.GetAiSpeechAssistantInfoByNumbersAsync(TestCallerNumber, TestDidNumber);

            assistant.ShouldNotBeNull();
            assistant.Name.ShouldBe("InfoAssistant");
            knowledge.ShouldNotBeNull();
            knowledge.Prompt.ShouldBe("Test prompt");
            userProfile.ShouldNotBeNull();
            userProfile.ProfileJson.ShouldContain("TestUser");
        });
    }

    [Fact]
    public async Task ShouldGetInboundRoutes_CallerSpecificOverFallback()
    {
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                From = TestCallerNumber, To = TestDidNumber, ForwardNumber = "+15551111111",
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                IsFallback = false, TimeZone = "Pacific Standard Time"
            });
            await repository.InsertAsync(new AiSpeechAssistantInboundRoute
            {
                To = TestDidNumber, ForwardNumber = "+15552222222",
                IsFullDay = true, DayOfWeek = "0,1,2,3,4,5,6", Priority = 1,
                IsFallback = true, TimeZone = "Pacific Standard Time"
            });
        });

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var routes = await dataProvider.GetAiSpeechAssistantInboundRouteAsync(TestCallerNumber, TestDidNumber, CancellationToken.None);

            routes.ShouldNotBeEmpty();
            routes.First().ForwardNumber.ShouldBe("+15551111111");
            routes.All(r => !r.IsFallback).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task ShouldGetHumanContact_ByAssistantId()
    {
        int assistantId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "ContactAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();
            assistantId = assistant.Id;

            await repository.InsertAsync(new AiSpeechAssistantHumanContact
            {
                AssistantId = assistant.Id, HumanPhone = "+15559990000"
            });
        });

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var contact = await dataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistantId, CancellationToken.None);

            contact.ShouldNotBeNull();
            contact.HumanPhone.ShouldBe("+15559990000");
        });
    }

    [Fact]
    public async Task ShouldGetFunctionCalls_ActiveByProvider()
    {
        int assistantId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "FuncAssistant", AnsweringNumber = TestDidNumber, ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy", IsDefault = true, IsDisplay = true
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();
            assistantId = assistant.Id;

            await repository.InsertAllAsync(new List<AiSpeechAssistantFunctionCall>
            {
                new()
                {
                    AssistantId = assistant.Id, Name = "ActiveFunc", Content = "{\"type\":\"function\"}",
                    Type = Messages.Enums.AiSpeechAssistant.AiSpeechAssistantSessionConfigType.Tool,
                    ModelProvider = RealtimeAiProvider.OpenAi, IsActive = true
                },
                new()
                {
                    AssistantId = assistant.Id, Name = "InactiveFunc", Content = "{\"type\":\"function\"}",
                    Type = Messages.Enums.AiSpeechAssistant.AiSpeechAssistantSessionConfigType.Tool,
                    ModelProvider = RealtimeAiProvider.OpenAi, IsActive = false
                },
                new()
                {
                    AssistantId = assistant.Id, Name = "WrongProvider", Content = "{\"type\":\"function\"}",
                    Type = Messages.Enums.AiSpeechAssistant.AiSpeechAssistantSessionConfigType.Tool,
                    ModelProvider = RealtimeAiProvider.Azure, IsActive = true
                }
            });
        });

        await Run<IAiSpeechAssistantDataProvider>(async dataProvider =>
        {
            var functions = await dataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
                new List<int> { assistantId }, RealtimeAiProvider.OpenAi, true);

            functions.Count.ShouldBe(1);
            functions.First().Name.ShouldBe("ActiveFunc");
        });
    }
}
