using Shouldly;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

public class CustomerItemsToolInstructionTests
{
    [Fact]
    public void AppendCustomerItemsToolInstructions_WithCustomerItemsTool_AppendsTriggerRule()
    {
        var prompt = AiSpeechAssistantConnectService.AppendCustomerItemsToolInstructions(
            "Base prompt.",
            [CustomerItemsTool()]);

        prompt.ShouldContain("Base prompt.");
        prompt.ShouldContain("immediately call query_customer_items_by_store_name");
        prompt.ShouldContain("mentions or corrects a store, restaurant, or shop name");
        prompt.ShouldContain("prefetch_only set to true");
        prompt.ShouldContain("Do not call this tool again unless the guest provides or corrects a different store name.");
    }

    [Fact]
    public void AppendCustomerItemsToolInstructions_WithoutCustomerItemsTool_ReturnsOriginalPrompt()
    {
        var prompt = AiSpeechAssistantConnectService.AppendCustomerItemsToolInstructions(
            "Base prompt.",
            [new AiSpeechAssistantFunctionCall
            {
                Name = OpenAiToolConstants.Hangup,
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi,
                IsActive = true
            }]);

        prompt.ShouldBe("Base prompt.");
    }

    [Fact]
    public void AppendCustomerItemsToolInstructions_WhenAlreadyAppended_DoesNotDuplicateRule()
    {
        var first = AiSpeechAssistantConnectService.AppendCustomerItemsToolInstructions(
            "Base prompt.",
            [CustomerItemsTool()]);
        var second = AiSpeechAssistantConnectService.AppendCustomerItemsToolInstructions(
            first,
            [CustomerItemsTool()]);

        second.ShouldBe(first);
    }

    [Fact]
    public void BuildCustomerItemsCacheSoldToIdCandidates_IncludesOriginalAndNormalizedIds()
    {
        var ids = AiSpeechAssistantConnectService.BuildCustomerItemsCacheSoldToIdCandidates(
            "000101/000102",
            "101");

        ids.ShouldBe(["000101", "101"]);
    }

    private static AiSpeechAssistantFunctionCall CustomerItemsTool() => new()
    {
        Name = OpenAiToolConstants.QueryCustomerItemsByStoreName,
        Type = AiSpeechAssistantSessionConfigType.Tool,
        ModelProvider = RealtimeAiProvider.OpenAi,
        IsActive = true
    };
}
