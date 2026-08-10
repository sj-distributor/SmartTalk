using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.Sales;
using Xunit;

namespace SmartTalk.UnitTests.Services.Sale;

public class SalesServiceBuildCustomerItemsStringTests
{
    [Fact]
    public async Task BuildCustomerItemsStringAsync_ShouldIncludeGoodsStatusAndUsePlantAndRtypeFromSourceDtos()
    {
        var crmClient = Substitute.For<ICrmClient>();
        var salesClient = Substitute.For<ISalesClient>();

        salesClient.GetAskInfoDetailListByCustomerAsync(Arg.Any<GetAskInfoDetailListByCustomerRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GetAskInfoDetailListByCustomerResponseDto
            {
                Data =
                [
                    new VwAskDetail
                    {
                        CustomerId = "C10001",
                        Material = "20022998CW",
                        Plant = "1200",
                        MaterialType = "ASK",
                        MaterialDesc = "Pork·BrandA·x·10kg·Belly"
                    }
                ]
            });

        salesClient.GetOrderHistoryByCustomerAsync(Arg.Any<GetOrderHistoryByCustomerRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GetOrderHistoryByCustomerResponseDto
            {
                Data =
                [
                    new SalesOrderHistoryDto
                    {
                        CustomerNumber = "C10001",
                        MaterialNumber = "30033999AB",
                        Plant = "1060",
                        MaterialType = "ORD",
                        MaterialDescription = "Beef·BrandB·x·5kg·Slice"
                    }
                ]
            });

        salesClient.QueryGoodsStatusAsync(Arg.Any<QueryGoodsStatusRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new QueryGoodsStatusResponseDto
            {
                ResultCode = 200,
                ResultData =
                [
                    new QueryGoodsStatusResultDto
                    {
                        Material = "20022998CW",
                        Plant = "1200",
                        Rtype = "ASK",
                        Status = "WAIT"
                    },
                    new QueryGoodsStatusResultDto
                    {
                        Material = "30033999AB",
                        Plant = "1060",
                        Rtype = "ORD",
                        Status = "NORMAL"
                    }
                ]
            });

        var service = new SalesService(crmClient, salesClient);

        var result = await service.BuildCustomerItemsStringAsync(["C10001"], CancellationToken.None);

        result.ShouldContain("status: WAIT");
        result.ShouldContain("status: NORMAL");

        await salesClient.Received(1).GetAskInfoDetailListByCustomerAsync(
            Arg.Is<GetAskInfoDetailListByCustomerRequestDto>(x =>
                x.CustomerNumbers.Count == 1 &&
                x.CustomerNumbers.Contains("C10001")),
            Arg.Any<CancellationToken>());

        await salesClient.Received(1).GetOrderHistoryByCustomerAsync(
            Arg.Is<GetOrderHistoryByCustomerRequestDto>(x =>
                string.IsNullOrWhiteSpace(x.CustomerNumber) &&
                x.CustomerNumbers.Count == 1 &&
                x.CustomerNumbers.Contains("C10001")),
            Arg.Any<CancellationToken>());

        await salesClient.Received(1).QueryGoodsStatusAsync(
            Arg.Is<QueryGoodsStatusRequestDto>(x =>
                x.List.Count == 2 &&
                x.List.Any(i => i.Material == "20022998CW" && i.Plant == "1200" && i.Rtype == "ASK") &&
                x.List.Any(i => i.Material == "30033999AB" && i.Plant == "1060" && i.Rtype == "ORD")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildCustomerItemsStringAsync_ShouldFetchCustomerItemsInSingleBatch()
    {
        var crmClient = Substitute.For<ICrmClient>();
        var salesClient = Substitute.For<ISalesClient>();

        salesClient.GetAskInfoDetailListByCustomerAsync(Arg.Any<GetAskInfoDetailListByCustomerRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GetAskInfoDetailListByCustomerResponseDto
            {
                Data =
                [
                    new VwAskDetail
                    {
                        CustomerId = "00010001",
                        Material = "20022998CW",
                        Plant = "1200",
                        MaterialType = "ASK",
                        MaterialDesc = "Pork·BrandA·x·10kg·Belly"
                    },
                    new VwAskDetail
                    {
                        CustomerId = "00010002",
                        Material = "20022999CW",
                        Plant = "1060",
                        MaterialType = "ASK",
                        MaterialDesc = "Chicken·BrandB·x·5kg·Wing"
                    }
                ]
            });

        salesClient.GetOrderHistoryByCustomerAsync(Arg.Any<GetOrderHistoryByCustomerRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GetOrderHistoryByCustomerResponseDto { Data = [] });

        salesClient.QueryGoodsStatusAsync(Arg.Any<QueryGoodsStatusRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new QueryGoodsStatusResponseDto { ResultCode = 200, ResultData = [] });

        var service = new SalesService(crmClient, salesClient);

        var result = await service.BuildCustomerItemsStringAsync([" 10001 ", "10002", "00010001"], CancellationToken.None);

        result.ShouldContain("PorkBelly");
        result.ShouldContain("ChickenWing");

        await salesClient.Received(1).GetAskInfoDetailListByCustomerAsync(
            Arg.Is<GetAskInfoDetailListByCustomerRequestDto>(x =>
                x.CustomerNumbers.Count == 2 &&
                x.CustomerNumbers.Contains("10001") &&
                x.CustomerNumbers.Contains("10002")),
            Arg.Any<CancellationToken>());

        await salesClient.Received(1).GetOrderHistoryByCustomerAsync(
            Arg.Is<GetOrderHistoryByCustomerRequestDto>(x =>
                string.IsNullOrWhiteSpace(x.CustomerNumber) &&
                x.CustomerNumbers.Count == 2 &&
                x.CustomerNumbers.Contains("10001") &&
                x.CustomerNumbers.Contains("10002")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildCustomerItemsStringsAsync_ShouldQuerySalesClientInBatchesOfTen()
    {
        var crmClient = Substitute.For<ICrmClient>();
        var salesClient = Substitute.For<ISalesClient>();
        var askRequests = new List<List<string>>();
        var orderRequests = new List<List<string>>();
        var customerIds = Enumerable.Range(1, 21).Select(x => x.ToString("00000")).ToList();

        salesClient.GetAskInfoDetailListByCustomerAsync(
                Arg.Do<GetAskInfoDetailListByCustomerRequestDto>(x => askRequests.Add(x.CustomerNumbers)),
                Arg.Any<CancellationToken>())
            .Returns(new GetAskInfoDetailListByCustomerResponseDto { Data = [] });

        salesClient.GetOrderHistoryByCustomerAsync(
                Arg.Do<GetOrderHistoryByCustomerRequestDto>(x => orderRequests.Add(x.CustomerNumbers)),
                Arg.Any<CancellationToken>())
            .Returns(new GetOrderHistoryByCustomerResponseDto { Data = [] });

        var service = new SalesService(crmClient, salesClient);

        await service.BuildCustomerItemsStringsAsync(customerIds, CancellationToken.None);

        askRequests.Count.ShouldBe(3);
        askRequests[0].ShouldBe(customerIds.Take(10).ToList());
        askRequests[1].ShouldBe(customerIds.Skip(10).Take(10).ToList());
        askRequests[2].ShouldBe(customerIds.Skip(20).ToList());

        orderRequests.Count.ShouldBe(3);
        orderRequests[0].ShouldBe(customerIds.Take(10).ToList());
        orderRequests[1].ShouldBe(customerIds.Skip(10).Take(10).ToList());
        orderRequests[2].ShouldBe(customerIds.Skip(20).ToList());
    }
}
