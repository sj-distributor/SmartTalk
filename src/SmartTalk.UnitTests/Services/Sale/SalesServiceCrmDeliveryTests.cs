using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.Crm;
using Xunit;

namespace SmartTalk.UnitTests.Services.Sale;

public class SalesServiceCrmDeliveryTests
{
    private readonly ICrmClient _crmClient = Substitute.For<ICrmClient>();
    private readonly ISalesClient _salesClient = Substitute.For<ISalesClient>();

    [Fact]
    public async Task BuildCrmCustomerInfoByPhoneAsync_ShouldIncludeRouteSchedule_WhenRouteConfigured()
    {
        var sut = new SalesService(_crmClient, _salesClient);
        var cancellationToken = CancellationToken.None;

        _crmClient.GetCrmTokenAsync(cancellationToken).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(
                Arg.Is<GetCustmoersByPhoneNumberRequestDto>(x => x.PhoneNumber == "+19164284295"),
                "crm-token",
                cancellationToken)
            .Returns([
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "109782",
                    CustomerName = "Test Customer",
                    Street = "Test Street",
                    Warehouse = "WH-A"
                }
            ]);

        _crmClient.GetDeliveryInfoByPhoneNumberAsync(Arg.Any<string>(), cancellationToken)
            .Returns(callInfo =>
            {
                var phone = callInfo.ArgAt<string>(0);
                return phone == "+19164284295"
                    ? [
                        new GetDeliveryInfoByPhoneNumberResponseDto
                        {
                            SapId = "109782",
                            RouteName = "ELKG",
                            DeliveryTime = "2、4、6",
                            EntryTime = "10:00",
                            LeaveTime = "18:00"
                        }
                    ]
                    : [];
            });

        var result = await sut.BuildCrmCustomerInfoByPhoneAsync("+1 (916) 428-4295", cancellationToken);

        result.ShouldContain("SAP编号: 109782");
        result.ShouldContain("路线1: ELKG");
        result.ShouldContain("送货安排: 每周二、周四、周六 10:00-18:00");
    }

    [Fact]
    public async Task BuildCrmCustomerInfoByPhoneAsync_ShouldMarkRouteUnconfigured_WhenNoRouteInfoReturned()
    {
        var sut = new SalesService(_crmClient, _salesClient);
        var cancellationToken = CancellationToken.None;

        _crmClient.GetCrmTokenAsync(cancellationToken).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(
                Arg.Any<GetCustmoersByPhoneNumberRequestDto>(),
                "crm-token",
                cancellationToken)
            .Returns([
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "109782",
                    CustomerName = "Test Customer"
                }
            ]);
        _crmClient.GetDeliveryInfoByPhoneNumberAsync(Arg.Any<string>(), cancellationToken)
            .Returns([]);

        var result = await sut.BuildCrmCustomerInfoByPhoneAsync("+19164284295", cancellationToken);

        result.ShouldContain("路线状态: 未配置路线");
    }

    [Fact]
    public async Task BuildCrmCustomerInfoByPhoneAsync_ShouldReturnUnrecognizedHint_WhenNoCrmCustomerMatched()
    {
        var sut = new SalesService(_crmClient, _salesClient);
        var cancellationToken = CancellationToken.None;

        _crmClient.GetCrmTokenAsync(cancellationToken).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(
                Arg.Any<GetCustmoersByPhoneNumberRequestDto>(),
                "crm-token",
                cancellationToken)
            .Returns([]);
        _crmClient.GetDeliveryInfoByPhoneNumberAsync(Arg.Any<string>(), cancellationToken)
            .Returns([]);

        var result = await sut.BuildCrmCustomerInfoByPhoneAsync("+19164284295", cancellationToken);

        result.ShouldContain("客户ID识别状态: 未识别到CRM-SAP ID");
        result.ShouldContain("建议回复: 可以先请客户提供客户编号或公司名称");
    }

    [Fact]
    public async Task BuildCrmCustomerInfoByPhoneAsync_ShouldQueryDeliveryByNormalizedPhoneOnly()
    {
        var sut = new SalesService(_crmClient, _salesClient);
        var cancellationToken = CancellationToken.None;

        _crmClient.GetCrmTokenAsync(cancellationToken).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(
                Arg.Any<GetCustmoersByPhoneNumberRequestDto>(),
                "crm-token",
                cancellationToken)
            .Returns([
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "109782",
                    CustomerName = "Test Customer"
                }
            ]);

        _crmClient.GetDeliveryInfoByPhoneNumberAsync(Arg.Any<string>(), cancellationToken)
            .Returns(callInfo =>
            {
                var phone = callInfo.ArgAt<string>(0);
                return phone == "+19164284295"
                    ? [
                        new GetDeliveryInfoByPhoneNumberResponseDto
                        {
                            SapId = "109782",
                            RouteName = "ELKG",
                            DeliveryTime = "1、2、3、4、5、6",
                            EntryTime = "10:00",
                            LeaveTime = "18:00"
                        }
                    ]
                    : [];
            });

        var result = await sut.BuildCrmCustomerInfoByPhoneAsync("+19164284295", cancellationToken);

        result.ShouldContain("路线1: ELKG");
        await _crmClient.Received().GetDeliveryInfoByPhoneNumberAsync("+19164284295", cancellationToken);
        await _crmClient.DidNotReceive().GetDeliveryInfoByPhoneNumberAsync("19164284295", cancellationToken);
        await _crmClient.DidNotReceive().GetDeliveryInfoByPhoneNumberAsync("9164284295", cancellationToken);
    }
}
