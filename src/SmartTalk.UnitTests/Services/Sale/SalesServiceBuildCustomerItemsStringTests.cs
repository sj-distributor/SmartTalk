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
    public async Task BuildCustomerItemsStringAsync_ShouldIncludeMaterialOverviewFields()
    {
        var crmClient = Substitute.For<ICrmClient>();
        var salesClient = Substitute.For<ISalesClient>();

        salesClient.GetCustomerMaterialOverviewAsync(Arg.Any<GetCustomerMaterialOverviewRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GetCustomerMaterialOverviewResponseDto
            {
                Code = 200,
                Data =
                [
                    new CustomerMaterialOverviewDto
                    {
                        CustomerNumber = "C10001",
                        Items =
                        [
                            new CustomerMaterialItemDto
                            {
                                SourceType = "AskInfo",
                                MaterialNumber = "20022998CW",
                                MaterialDescription = "Pork·BrandA·x·10kg·Belly",
                                Plant = "1200",
                                MaterialType = "ASK",
                                LevelCode5 = "L5001",
                                BaseUnit = "LB",
                                SalesUnit = "CS",
                                Weight = 10.5m,
                                PlaceOfOrigin = "US",
                                Packing = "10 LB/CS",
                                Specifications = "Spec A",
                                Rank = "A",
                                Atr = 128,
                                GoodsStatus = "WAIT"
                            },
                            new CustomerMaterialItemDto
                            {
                                SourceType = "History",
                                MaterialNumber = "30033999AB",
                                MaterialDescription = "Beef·BrandB·x·5kg·Slice",
                                Plant = "1060",
                                MaterialType = "ORD",
                                LevelCode5 = "L5002",
                                BaseUnit = "KG",
                                SalesUnit = "BOX",
                                Weight = 5,
                                Rank = "B",
                                Atr = 64,
                                GoodsStatus = "NORMAL",
                                LastInvoiceDate = new DateTime(2026, 7, 31)
                            }
                        ],
                        Level5Habits =
                        [
                            new CustomerMaterialLevel5HabitDto
                            {
                                LevelCode5 = "L5001",
                                CustomerLikeNames =
                                [
                                    new CustomerLikeNameDto
                                    {
                                        CreateDate = new DateTime(2026, 5, 1),
                                        CustomerLikeName = "preferred pork"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });

        var service = new SalesService(crmClient, salesClient);

        var result = await service.BuildCustomerItemsStringAsync(["C10001"], CancellationToken.None);

        result.ShouldContain("PorkBelly");
        result.ShouldContain("Brand: BrandA");
        result.ShouldContain("Aliases: preferred pork");
        result.ShouldContain("status: WAIT");
        result.ShouldContain("baseUnit: LB");
        result.ShouldContain("salesUnit: CS");
        result.ShouldContain("weights: 10.5");
        result.ShouldContain("placeOfOrigin: US");
        result.ShouldContain("packing: 10 LB/CS");
        result.ShouldContain("specifications: Spec A");
        result.ShouldContain("ranks: A");
        result.ShouldContain("atr: 128");
        result.ShouldContain("status: NORMAL");

        await salesClient.Received(1).GetCustomerMaterialOverviewAsync(
            Arg.Is<GetCustomerMaterialOverviewRequestDto>(x =>
                x.CustomerNumbers.Count == 1 &&
                x.CustomerNumbers.Contains("C10001")),
            Arg.Any<CancellationToken>());

        _ = salesClient.DidNotReceiveWithAnyArgs().GetAskInfoDetailListByCustomerAsync(default, default);
        _ = salesClient.DidNotReceiveWithAnyArgs().GetOrderHistoryByCustomerAsync(default, default);
        _ = salesClient.DidNotReceiveWithAnyArgs().GetCustomerLevel5HabitAsync(default, default);
        _ = salesClient.DidNotReceiveWithAnyArgs().QueryGoodsStatusAsync(default, default);
    }

    [Fact]
    public async Task BuildCustomerItemsStringAsync_ShouldFetchCustomerItemsInSingleBatch()
    {
        var crmClient = Substitute.For<ICrmClient>();
        var salesClient = Substitute.For<ISalesClient>();

        salesClient.GetCustomerMaterialOverviewAsync(Arg.Any<GetCustomerMaterialOverviewRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GetCustomerMaterialOverviewResponseDto
            {
                Code = 200,
                Data =
                [
                    new CustomerMaterialOverviewDto
                    {
                        CustomerNumber = "00010001",
                        Items =
                        [
                            new CustomerMaterialItemDto
                            {
                                MaterialNumber = "20022998CW",
                                MaterialDescription = "Pork·BrandA·x·10kg·Belly"
                            }
                        ]
                    },
                    new CustomerMaterialOverviewDto
                    {
                        CustomerNumber = "00010002",
                        Items =
                        [
                            new CustomerMaterialItemDto
                            {
                                MaterialNumber = "20022999CW",
                                MaterialDescription = "Chicken·BrandB·x·5kg·Wing"
                            }
                        ]
                    }
                ]
            });

        var service = new SalesService(crmClient, salesClient);

        var result = await service.BuildCustomerItemsStringAsync([" 10001 ", "10002", "00010001"], CancellationToken.None);

        result.ShouldContain("PorkBelly");
        result.ShouldContain("ChickenWing");

        await salesClient.Received(1).GetCustomerMaterialOverviewAsync(
            Arg.Is<GetCustomerMaterialOverviewRequestDto>(x =>
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
        var overviewRequests = new List<List<string>>();
        var customerIds = Enumerable.Range(1, 21).Select(x => x.ToString("00000")).ToList();

        salesClient.GetCustomerMaterialOverviewAsync(
                Arg.Do<GetCustomerMaterialOverviewRequestDto>(x => overviewRequests.Add(x.CustomerNumbers)),
                Arg.Any<CancellationToken>())
            .Returns(new GetCustomerMaterialOverviewResponseDto { Code = 200, Data = [] });

        var service = new SalesService(crmClient, salesClient);

        await service.BuildCustomerItemsStringsAsync(customerIds, CancellationToken.None);

        overviewRequests.Count.ShouldBe(3);
        overviewRequests[0].ShouldBe(customerIds.Take(10).ToList());
        overviewRequests[1].ShouldBe(customerIds.Skip(10).Take(10).ToList());
        overviewRequests[2].ShouldBe(customerIds.Skip(20).ToList());
    }
}
