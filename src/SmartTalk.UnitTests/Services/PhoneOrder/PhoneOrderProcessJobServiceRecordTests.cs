using Shouldly;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderProcessJobServiceRecordTests
{
    [Fact]
    public void AddCustomerIdToTranscriptionText_ShouldPrefixMatchedCustomerId()
    {
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Name = "12345/67890"
        };

        var result = PhoneOrderProcessJobService.AddCustomerIdToTranscriptionText(
            "內容摘要：已完成下單",
            assistant,
            "12345");

        result.ShouldStartWith("客人ID：12345");
        result.ShouldContain("內容摘要：已完成下單");
    }

    [Fact]
    public void AddCustomerIdToTranscriptionText_ShouldShowNotMatchedWhenMultipleIdsRemainUnresolved()
    {
        var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Name = "12345/67890"
        };

        var result = PhoneOrderProcessJobService.AddCustomerIdToTranscriptionText(
            "內容摘要：未能確認客戶",
            assistant,
            string.Empty);

        result.ShouldStartWith("客人ID：未匹配到");
        result.ShouldContain("內容摘要：未能確認客戶");
    }

    [Fact]
    public void UpdateCustomerIdLineInReport_ShouldReplaceTheExistingCustomerIdLine()
    {
        var result = PhoneOrderProcessJobService.UpdateCustomerIdLineInReport(
            "來電號碼：+14084026529\n客人ID：\n內容摘要：已下單",
            "12345");

        result.ShouldBe("來電號碼：+14084026529\n客人ID：12345\n內容摘要：已下單");
    }
}
