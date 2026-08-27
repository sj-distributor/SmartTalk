using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderProcessJobServiceAiModelTests
{
    [Fact]
    public void ResolveAiModel_ShouldReturnSmartalk()
    {
        var result = PhoneOrderProcessJobService.ResolveAiModel();

        Assert.Equal("Smartalk", result);
    }

    [Fact]
    public void ResolveOrderSource_ShouldReturnOmePhoneSemiAutomatic_ForAixvolinkSource()
    {
        var result = PhoneOrderProcessJobService.ResolveOrderSource(PhoneOrderSourceProviders.Aixvolink);

        Assert.Equal("OME PHONE 半自动", result);
    }

    [Fact]
    public void ResolveOrderSource_ShouldReturnOmePhoneAutomatic_ForNonAixvolinkSource()
    {
        var result = PhoneOrderProcessJobService.ResolveOrderSource("SpeechMatics");

        Assert.Equal("OME PHONE 全自动", result);
    }
}
