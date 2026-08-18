using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderProcessJobServiceAiModelTests
{
    [Fact]
    public void ResolveAiModel_ShouldReturnOmePhoneAutomatic_ForAixvolinkSource()
    {
        var result = PhoneOrderProcessJobService.ResolveAiModel(PhoneOrderSourceProviders.Aixvolink);

        Assert.Equal("OME PHONE 半自动", result);
    }

    [Fact]
    public void ResolveAiModel_ShouldReturnOmePhoneSemiAutomatic_ForNonAixvolinkSource()
    {
        var result = PhoneOrderProcessJobService.ResolveAiModel("SpeechMatics");

        Assert.Equal("OME PHONE 全自动", result);
    }
}
