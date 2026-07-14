using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.Crm;
using Xunit;

namespace SmartTalk.UnitTests.Services.Sale;

public class SalesCustomerMatchServiceTests
{
    private readonly ICrmClient _crmClient = Substitute.For<ICrmClient>();
    private readonly IDaovikaClient _daovikaClient = Substitute.For<IDaovikaClient>();

    [Fact]
    public async Task MatchCustomerAsync_ShouldReturnPhoneMatchedCustomer_WhenCrmPhoneMatched()
    {
        var sut = new SalesCustomerMatchService(_crmClient, _daovikaClient);

        _crmClient.GetCrmTokenAsync(Arg.Any<CancellationToken>()).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(
                Arg.Is<GetCustmoersByPhoneNumberRequestDto>(x => x.PhoneNumber == "+19164284295"),
                "crm-token",
                Arg.Any<CancellationToken>())
            .Returns([
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "00012345",
                    CustomerName = "Test Customer"
                }
            ]);
        _crmClient.GetCustomersByPhoneNumberAsync(
                Arg.Is<GetCustmoersByPhoneNumberRequestDto>(x => x.PhoneNumber == "+19165550000"),
                "crm-token",
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await sut.MatchCustomerAsync("+1 (916) 428-4295", "+1 (916) 555-0000", "Test Store", ["+1 (916) 555-0000"], CancellationToken.None);

        result.SoldToId.ShouldBe("12345");
        result.SoldToIds.ShouldBe(["12345"]);
    }

    [Fact]
    public async Task MatchCustomerAsync_ShouldFallbackToStoreName_WhenPhoneNotMatched()
    {
        var sut = new SalesCustomerMatchService(_crmClient, _daovikaClient);

        _crmClient.GetCrmTokenAsync(Arg.Any<CancellationToken>()).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(Arg.Any<GetCustmoersByPhoneNumberRequestDto>(), "crm-token", Arg.Any<CancellationToken>())
            .Returns([]);
        _crmClient.GetCustomerIdsByShopNameAsync("Lucky House", Arg.Any<CancellationToken>())
            .Returns([
                new GetCustomerIdByShopNameResponseDto
                {
                    SapId = "00098765",
                    CustomerName = "Lucky House"
                }
            ]);

        var result = await sut.MatchCustomerAsync("+1 916 000 0000", "+1 916 111 1111", "Lucky House", ["+1 916 111 1111"], CancellationToken.None);

        result.SoldToId.ShouldBe("98765");
        result.SoldToIds.ShouldBe(["98765"]);
    }

    [Fact]
    public async Task MatchCustomerAsync_ShouldFallbackToSalesGroup_WhenCustomerIdNotMatched()
    {
        var sut = new SalesCustomerMatchService(_crmClient, _daovikaClient);

        _crmClient.GetCrmTokenAsync(Arg.Any<CancellationToken>()).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(Arg.Any<GetCustmoersByPhoneNumberRequestDto>(), "crm-token", Arg.Any<CancellationToken>())
            .Returns([]);
        _crmClient.GetCustomerIdsByShopNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _daovikaClient.GetSalesGroupByPhoneNumberAsync("+19164284295", Arg.Any<CancellationToken>())
            .Returns("SG-001");

        var result = await sut.MatchCustomerAsync("+1 (916) 428-4295", null, null, ["+1 (916) 428-4295"], CancellationToken.None);

        result.SoldToId.ShouldBeEmpty();
        result.SoldToIds.ShouldBeEmpty();
        result.SalesGroup.ShouldBe("SG-001");
    }

    [Fact]
    public async Task MatchCustomerAsync_ShouldSkipCustomerMatchingAndFallbackToSalesGroup_WhenCrmUnavailable()
    {
        var sut = new SalesCustomerMatchService(_crmClient, _daovikaClient);

        _crmClient.GetCrmTokenAsync(Arg.Any<CancellationToken>()).Returns<Task<string>>(_ => throw new Exception("crm unavailable"));
        _daovikaClient.GetSalesGroupByPhoneNumberAsync("+19164284295", Arg.Any<CancellationToken>())
            .Returns("SG-001");

        var result = await sut.MatchCustomerAsync("+1 (916) 428-4295", null, "Lucky House", ["+1 (916) 428-4295"], CancellationToken.None);

        result.SoldToId.ShouldBeEmpty();
        result.SoldToIds.ShouldBeEmpty();
        result.SalesGroup.ShouldBe("SG-001");
        await _crmClient.DidNotReceive().GetCustomersByPhoneNumberAsync(Arg.Any<GetCustmoersByPhoneNumberRequestDto>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _crmClient.DidNotReceive().GetCustomerIdsByShopNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MatchCustomerAsync_ShouldTryBothPhoneNumbers_WhenFallbackToSalesGroup()
    {
        var sut = new SalesCustomerMatchService(_crmClient, _daovikaClient);

        _crmClient.GetCrmTokenAsync(Arg.Any<CancellationToken>()).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(Arg.Any<GetCustmoersByPhoneNumberRequestDto>(), "crm-token", Arg.Any<CancellationToken>())
            .Returns([]);
        _crmClient.GetCustomerIdsByShopNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _daovikaClient.GetSalesGroupByPhoneNumberAsync("+19164284295", Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        _daovikaClient.GetSalesGroupByPhoneNumberAsync("+19165550000", Arg.Any<CancellationToken>())
            .Returns("SG-002");

        var result = await sut.MatchCustomerAsync("+1 (916) 428-4295", "+1 (916) 555-0000", null, ["+1 (916) 428-4295", "+1 (916) 555-0000"], CancellationToken.None);

        result.SalesGroup.ShouldBe("SG-002");
        await _daovikaClient.Received(1).GetSalesGroupByPhoneNumberAsync("+19164284295", Arg.Any<CancellationToken>());
        await _daovikaClient.Received(1).GetSalesGroupByPhoneNumberAsync("+19165550000", Arg.Any<CancellationToken>());
    }
}
