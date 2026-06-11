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

        salesClient.GetCustomerAiQuotationAsync(Arg.Any<GetCustomerAiQuotationRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new GetCustomerAiQuotationResponseDto
            {
                Code = 200,
                AiQuotationList =
                [
                    new AiQuotationDto
                    {
                        MaterialId = "20022998CW",
                        SjAiCost = 12.5,
                        KfOrOsAiCost = 10
                    },
                    new AiQuotationDto
                    {
                        MaterialId = "30033999AB",
                        SjAiCost = 8.25
                    }
                ]
            });

        var service = new SalesService(crmClient, salesClient);

        var result = await service.BuildCustomerItemsStringAsync(["C10001"], CancellationToken.None);

        result.ShouldContain("status: WAIT");
        result.ShouldContain("status: NORMAL");
        result.ShouldContain("price: SJ价 12.5，KF/OS价 10");
        result.ShouldContain("price: SJ价 8.25");
        result.ShouldNotContain("materialId", Case.Sensitive);

        await salesClient.Received(1).QueryGoodsStatusAsync(
            Arg.Is<QueryGoodsStatusRequestDto>(x =>
                x.List.Count == 2 &&
                x.List.Any(i => i.Material == "20022998CW" && i.Plant == "1200" && i.Rtype == "ASK") &&
                x.List.Any(i => i.Material == "30033999AB" && i.Plant == "1060" && i.Rtype == "ORD")),
            Arg.Any<CancellationToken>());

        await salesClient.Received(1).GetCustomerAiQuotationAsync(
            Arg.Is<GetCustomerAiQuotationRequestDto>(x =>
                x.CustomerId == "C10001" &&
                x.MaterialIdList.Count == 2 &&
                x.MaterialIdList.Contains("20022998CW") &&
                x.MaterialIdList.Contains("30033999AB")),
            Arg.Any<CancellationToken>());
    }
}
