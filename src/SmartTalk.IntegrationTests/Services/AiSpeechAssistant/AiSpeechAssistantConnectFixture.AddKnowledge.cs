using Autofac;
using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.EventHandling;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldAddKnowledgeWithTextAndFileDetails_AndGeneratePrompt()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        var fileUrl = $"https://smartiestest.oss-cn-hongkong.aliyuncs.com/20260403/0ac08586-0e4c-4fd8-a2e9-0a1bac7e879c.pdf?Expires=253402300799&OSSAccessKeyId=LTAI5tEYyDT8YqJBSXaFDtyk&Signature=O%2B9TEJ5N0udE4CsgbJEHA095xAM%3D";
        var extractedFileText = $"Extracted file content for {caseId}";
        var textDetailContent = $"Text knowledge content for {caseId}";

        int assistantId = 1;
        int previousKnowledgeId = 1;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = $"KnowledgeAssistant-{caseId}",
                AnsweringNumber = $"+1555{caseId[..6]}",
                ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy",
                IsDefault = true,
                IsDisplay = true,
                ModelLanguage = "English"
            };
            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            assistantId = assistant.Id;

            var previousKnowledge = new AiSpeechAssistantKnowledge
            {
                AssistantId = assistantId,
                Json = "{\"old_key\":\"old_value\"}",
                Prompt = "old prompt",
                IsActive = true,
                Version = "1.0",
                Greetings = "hello old",
                CreatedBy = 1,
                ModelLanguage = "English"
            };
            await repository.InsertAsync(previousKnowledge);

            previousKnowledgeId = previousKnowledge.Id;
        });

        AddAiSpeechAssistantKnowledgeResponse response = null;

        await Run<IMediator>(async mediator =>
        {
            var command = new AddAiSpeechAssistantKnowledgeCommand
            {
                AssistantId = assistantId,
                Json = $"{{\"base_key\":\"base_value_{caseId}\"}}",
                Language = "English",
                Greetings = $"hello new {caseId}",
                Details =
                [
                    new AiSpeechAssistantKnowledgeDetailDto
                    {
                        KnowledgeName = "TextDetail",
                        FormatType = AiSpeechAssistantKonwledgeFormatType.Text,
                        Content = textDetailContent
                    },
                    new AiSpeechAssistantKnowledgeDetailDto
                    {
                        KnowledgeName = "FileDetail",
                        FormatType = AiSpeechAssistantKonwledgeFormatType.FIle,
                        Content = fileUrl,
                        FileName = "menu.pdf"
                    }
                ]
            };

            response = await mediator.SendAsync<AddAiSpeechAssistantKnowledgeCommand, AddAiSpeechAssistantKnowledgeResponse>(command);
        }, builder =>
        {
            var extractor = Substitute.For<IFileTextExtractor>();
            extractor.ExtractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => call.ArgAt<string>(0));
            extractor.ExtractAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => call.ArgAt<string>(0) == fileUrl ? extractedFileText : call.ArgAt<string>(0));

            var eventHandlingService = Substitute.For<IEventHandlingService>();
            eventHandlingService.HandlingEventAsync(Arg.Any<AiSpeechAssistantKnowledgeAddedEvent>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            builder.RegisterInstance(extractor).As<IFileTextExtractor>();
            builder.RegisterInstance(eventHandlingService).As<IEventHandlingService>();
        });

        response.ShouldNotBeNull();
        response.Data.ShouldNotBeNull();
        response.Data.Details.Count.ShouldBe(2);
        response.Data.Version.ShouldBe("1.1");
        response.Data.Prompt.ShouldContain("TextDetail：");
        response.Data.Prompt.ShouldContain(textDetailContent);
        response.Data.Prompt.ShouldContain("FileDetail：");
        response.Data.Prompt.ShouldContain(extractedFileText);
        response.Data.Prompt.ShouldNotContain(fileUrl);

        await Run<IRepository>(async repository =>
        {
            var previous = await repository.Query<AiSpeechAssistantKnowledge>()
                .FirstOrDefaultAsync(x => x.Id == previousKnowledgeId);
            previous.ShouldNotBeNull();
            previous.IsActive.ShouldBeFalse();

            var latest = await repository.Query<AiSpeechAssistantKnowledge>()
                .FirstOrDefaultAsync(x => x.Id == response.Data.Id);
            latest.ShouldNotBeNull();
            latest.AssistantId.ShouldBe(assistantId);
            latest.IsActive.ShouldBeTrue();
            latest.Version.ShouldBe("1.1");
            latest.Prompt.ShouldContain(textDetailContent);
            latest.Prompt.ShouldContain(extractedFileText);
            latest.Prompt.ShouldContain($"Base_Key： base_value_{caseId}");

            var persistedDetails = await repository.Query<AiSpeechAssistantKnowledgeDetail>()
                .Where(x => x.KnowledgeId == latest.Id)
                .ToListAsync();
            persistedDetails.Count.ShouldBe(2);
            persistedDetails.Any(x => x.KnowledgeName == "TextDetail" && x.Content == textDetailContent).ShouldBeTrue();
            persistedDetails.Any(x =>
                x.KnowledgeName == "FileDetail" &&
                x.Content == fileUrl &&
                x.FileName == "menu.pdf").ShouldBeTrue();
        });
    }
}
