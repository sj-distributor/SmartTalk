using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

public class CallQuestionPromptTests
{
    [Fact]
    public void V2Prompt_ShouldKeepKnowledgePromptAndAppendCallQuestion()
    {
        var prompt = AiSpeechAssistantConnectService.AppendCallQuestion(
            "Managed greeting and service rules.",
            "1. Is a table available tonight?\n2. How long is the wait?");

        prompt.ShouldStartWith("Managed greeting and service rules.");
        prompt.ShouldContain("For this call, ask the merchant the following customer questions in order:");
        prompt.ShouldContain("1. Is a table available tonight?");
        prompt.ShouldContain("2. How long is the wait?");
    }

    [Fact]
    public void V2Prompt_ShouldReturnKnowledgePrompt_WhenCallQuestionIsEmpty()
    {
        AiSpeechAssistantConnectService.AppendCallQuestion("Managed prompt.", null)
            .ShouldBe("Managed prompt.");
    }

    [Fact]
    public void V1Prompt_ShouldKeepKnowledgePromptAndAppendCallQuestion()
    {
        var prompt = AiSpeechAssistantService.AppendCallQuestion(
            "Managed greeting and service rules.",
            "1. Is a table available tonight?");

        prompt.ShouldStartWith("Managed greeting and service rules.");
        prompt.ShouldContain("For this call, ask the merchant the following customer questions in order:");
        prompt.ShouldContain("1. Is a table available tonight?");
    }
}
