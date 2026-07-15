using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.Daovika;
using SmartTalk.Messages.Dto.Daovika;
using Xunit;

namespace SmartTalk.UnitTests.Services.Http.Clients;

public class DaovikaClientTests
{
    private readonly ISmartTalkHttpClientFactory _httpClientFactory = Substitute.For<ISmartTalkHttpClientFactory>();

    [Fact]
    public async Task GetSalesGroupByPhoneNumberAsync_ShouldQueryDaovikaTableWithPhone()
    {
        var setting = BuildSetting();
        var sut = new DaovikaClient(setting, _httpClientFactory);

        _httpClientFactory.GetAsync<GetSalesGroupByPhoneNumberResponseDto>(
                Arg.Is<string>(url =>
                    url.Contains("tableId=fc7a74fc-ea1f-4be1-93c3-03ed190a2c56", StringComparison.Ordinal) &&
                    url.Contains("apiKey=api-key", StringComparison.Ordinal) &&
                    url.Contains("field=phoneNumber&op=eq", StringComparison.Ordinal) &&
                    url.Contains("value=%2B19164284295", StringComparison.Ordinal) &&
                    url.Contains("limit=1000&offset=0", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>(),
                headers: Arg.Is<Dictionary<string, string>>(headers =>
                    headers["accept"] == "application/json" &&
                    headers["x-api-key"] == "api-key"))
            .Returns(new GetSalesGroupByPhoneNumberResponseDto
            {
                Rows =
                [
                    new SalesGroupRowDto
                    {
                        SalesGroup = " SG-001 "
                    }
                ]
            });

        var result = await sut.GetSalesGroupByPhoneNumberAsync("+19164284295", CancellationToken.None);

        result.ShouldBe("SG-001");
    }

    private static DaovikaSetting BuildSetting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Daovika:BaseUrl", "https://daovika.example.com" },
                { "Daovika:ApiKey", "api-key" }
            })
            .Build();

        return new DaovikaSetting(configuration);
    }
}
