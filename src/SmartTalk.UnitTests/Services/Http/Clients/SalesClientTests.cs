using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Dto.Sales;
using Xunit;

namespace SmartTalk.UnitTests.Services.Http.Clients;

public class SalesClientTests
{
    private readonly ISmartTalkHttpClientFactory _httpClientFactory = Substitute.For<ISmartTalkHttpClientFactory>();

    [Fact]
    public async Task GetCustomerMaterialOverviewAsync_ShouldPostNormalizedCustomerNumbersOnce()
    {
        var capturedRequests = new List<GetCustomerMaterialOverviewRequestDto>();
        var sut = BuildClient();

        _httpClientFactory.PostAsJsonAsync<GetCustomerMaterialOverviewResponseDto>(
                "https://sales.example.com/api/SalesOrder/GetCustomerMaterialOverview",
                Arg.Do<object>(value => capturedRequests.Add((GetCustomerMaterialOverviewRequestDto)value)),
                Arg.Any<CancellationToken>(),
                headers: Arg.Any<Dictionary<string, string>>())
            .Returns(new GetCustomerMaterialOverviewResponseDto { Code = 200, Data = [] });

        await sut.GetCustomerMaterialOverviewAsync(
            new GetCustomerMaterialOverviewRequestDto { CustomerNumbers = [" 00001 ", "00002", "00001", ""] },
            CancellationToken.None);

        capturedRequests.Count.ShouldBe(1);
        capturedRequests[0].CustomerNumbers.ShouldBe(new List<string> { "00001", "00002" });
    }

    [Fact]
    public async Task GetAskInfoDetailListByCustomerAsync_ShouldPostCustomerNumbersOnce()
    {
        var capturedRequests = new List<GetAskInfoDetailListByCustomerRequestDto>();
        var sut = BuildClient();
        var customerIds = new List<string> { "00001", "00002" };

        _httpClientFactory.PostAsJsonAsync<GetAskInfoDetailListByCustomerResponseDto>(
                "https://sales.example.com/api/SalesOrder/GetAskInfoDetailListByCustomer",
                Arg.Do<object>(value => capturedRequests.Add((GetAskInfoDetailListByCustomerRequestDto)value)),
                Arg.Any<CancellationToken>(),
                headers: Arg.Any<Dictionary<string, string>>())
            .Returns(new GetAskInfoDetailListByCustomerResponseDto { Data = [] });

        await sut.GetAskInfoDetailListByCustomerAsync(
            new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = customerIds },
            CancellationToken.None);

        capturedRequests.Count.ShouldBe(1);
        capturedRequests[0].CustomerNumbers.ShouldBe(customerIds);
    }

    [Fact]
    public async Task GetOrderHistoryByCustomerAsync_ShouldPostCustomerNumbersOnce()
    {
        var capturedRequests = new List<GetOrderHistoryByCustomerRequestDto>();
        var sut = BuildClient();
        var customerIds = new List<string> { "00001", "00002" };

        _httpClientFactory.PostAsJsonAsync<GetOrderHistoryByCustomerResponseDto>(
                "https://sales.example.com/api/SalesOrder/GetOrderHistoryByCustomer",
                Arg.Do<object>(value => capturedRequests.Add((GetOrderHistoryByCustomerRequestDto)value)),
                Arg.Any<CancellationToken>(),
                headers: Arg.Any<Dictionary<string, string>>())
            .Returns(new GetOrderHistoryByCustomerResponseDto { Data = [] });

        await sut.GetOrderHistoryByCustomerAsync(
            new GetOrderHistoryByCustomerRequestDto { CustomerNumbers = customerIds },
            CancellationToken.None);

        capturedRequests.Count.ShouldBe(1);
        capturedRequests[0].CustomerNumbers.ShouldBe(customerIds);
    }

    private SalesClient BuildClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Sales:BaseUrl", "https://sales.example.com" },
                { "Sales:ApiKey", "sales-api-key" },
                { "SalesOrderArrival:BaseUrl", "https://arrival.example.com" },
                { "SalesOrderArrival:ApiKey", "arrival-api-key" },
                { "SalesOrderArrival:OrganizationId", "org-id" },
                { "SalesCustomerHabit:BaseUrl", "https://habit.example.com" },
                { "SalesCustomerHabit:ApiKey", "habit-api-key" }
            })
            .Build();

        return new SalesClient(
            new SalesSetting(configuration),
            _httpClientFactory,
            new SalesOrderArrivalSetting(configuration),
            new SalesCustomerHabitSetting(configuration));
    }
}
