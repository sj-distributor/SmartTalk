using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeAiServiceValidationTests : RealtimeAiServiceTestBase
{
    [Fact]
    public async Task ConnectAsync_NullOptions_ThrowsArgumentNullException()
    {
        var ex = await Should.ThrowAsync<ArgumentNullException>(
            () => Sut.ConnectAsync(null!, CancellationToken.None));
        ex.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task ConnectAsync_NullClientConfig_ThrowsArgumentNullException()
    {
        var options = CreateDefaultOptions(o => o.ClientConfig = null!);

        var ex = await Should.ThrowAsync<ArgumentNullException>(
            () => Sut.ConnectAsync(options, CancellationToken.None));
        ex.ParamName.ShouldContain("ClientConfig");
    }

    [Fact]
    public async Task ConnectAsync_NullModelConfig_ThrowsArgumentNullException()
    {
        var options = CreateDefaultOptions(o => o.ModelConfig = null!);

        var ex = await Should.ThrowAsync<ArgumentNullException>(
            () => Sut.ConnectAsync(options, CancellationToken.None));
        ex.ParamName.ShouldContain("ModelConfig");
    }

    [Fact]
    public async Task ConnectAsync_NullServiceUrl_ThrowsArgumentNullException()
    {
        var options = CreateDefaultOptions(o => o.ModelConfig.ServiceUrl = null!);

        var ex = await Should.ThrowAsync<ArgumentNullException>(
            () => Sut.ConnectAsync(options, CancellationToken.None));
        ex.ParamName.ShouldContain("ServiceUrl");
    }

    [Fact]
    public async Task ConnectAsync_NullConnectionProfile_ThrowsArgumentNullException()
    {
        var options = CreateDefaultOptions(o => o.ConnectionProfile = null!);

        var ex = await Should.ThrowAsync<ArgumentNullException>(
            () => Sut.ConnectAsync(options, CancellationToken.None));
        ex.ParamName.ShouldContain("ConnectionProfile");
    }
}
