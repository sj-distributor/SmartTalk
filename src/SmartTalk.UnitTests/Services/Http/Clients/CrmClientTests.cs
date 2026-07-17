using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.Crm;
using SmartTalk.Messages.Dto.Sales;
using Xunit;

namespace SmartTalk.UnitTests.Services.Http.Clients;

public class CrmClientTests
{
    [Fact]
    public async Task GetChangedSalesAutoSyncCustomersAsync_WhenHttpRequestFails_ShouldThrow()
    {
        var httpClient = Substitute.For<ISmartTalkHttpClientFactory>();
        httpClient.GetAsync<List<CrmSalesAutoSyncCustomerDto>>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<bool>())
            .Returns((List<CrmSalesAutoSyncCustomerDto>)null!);

        var sut = new CrmClient(httpClient, BuildCrmSetting());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetChangedSalesAutoSyncCustomersAsync(CancellationToken.None));

        Assert.Contains("Failed to load changed CRM sales auto sync customers", ex.Message);
    }

    [Fact]
    public async Task GetSalesAutoSyncCustomersAsync_WhenHttpRequestFails_ShouldThrow()
    {
        var httpClient = Substitute.For<ISmartTalkHttpClientFactory>();
        httpClient.GetAsync<CrmSalesAutoSyncPagedResponseDto>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<bool>())
            .Returns((CrmSalesAutoSyncPagedResponseDto)null!);

        var sut = new CrmClient(httpClient, BuildCrmSetting());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetSalesAutoSyncCustomersAsync(cancellationToken: CancellationToken.None));

        Assert.Contains("Failed to load CRM sales auto sync customers", ex.Message);
    }

    private static CrmSetting BuildCrmSetting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Crm:BaseUrl"] = "https://crm.example.com",
                ["Crm:ClientId"] = "client-id",
                ["Crm:ClientSecret"] = "client-secret",
                ["Crm:SyncBaseUrl"] = "https://crmv3-api.testomenow.com",
                ["Crm:ApiKey"] = "api-key"
            })
            .Build();

        return new CrmSetting(configuration);
    }
}
