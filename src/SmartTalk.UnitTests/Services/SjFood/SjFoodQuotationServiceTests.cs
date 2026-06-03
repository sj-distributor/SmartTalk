using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.SjFood;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Crm;
using SmartTalk.Messages.Dto.SjFood;
using Xunit;

namespace SmartTalk.UnitTests.Services.SjFood;

public class SjFoodQuotationServiceTests
{
    private readonly ICrmClient _crmClient = Substitute.For<ICrmClient>();
    private readonly ISjFoodClient _sjFoodClient = Substitute.For<ISjFoodClient>();

    // 多个 CRM 客户共享同一个电话时，验证可以用 CRM 已返回的字段唯一定位 SAPID：
    // customer_hint 原文、消费者姓名、Header note、街道/品牌街道、仓库号、联系人姓名/身份。
    [Theory]
    [InlineData(null, "Alice Chen", null, null, null, null, null, "100001")]
    [InlineData("VIP hotpot", null, null, null, null, null, null, "100002")]
    [InlineData(null, null, "VIP hotpot", null, null, null, null, "100002")]
    [InlineData(null, null, null, "MAINSTREET", null, null, null, "100003")]
    [InlineData(null, null, null, null, "1600", null, null, "100004")]
    [InlineData(null, null, null, null, null, "manager", "amy", "100005")]
    public async Task QueryPriceByPhoneAndProductAsync_ShouldDisambiguateMultipleCustomersByCrmFields(
        string customerHint,
        string customerNameHint,
        string headerNoteHint,
        string streetHint,
        string warehouseHint,
        string contactIdentityHint,
        string contactNameHint,
        string expectedSapId)
    {
        var sut = new SjFoodQuotationService(_crmClient, _sjFoodClient);
        var cancellationToken = CancellationToken.None;

        _crmClient.GetCrmTokenAsync(cancellationToken).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(Arg.Any<GetCustmoersByPhoneNumberRequestDto>(), "crm-token", cancellationToken)
            .Returns([
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "100001",
                    CustomerName = "Alice Chen"
                },
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "100002",
                    HeaderNote1 = "VIP Hotpot account"
                },
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "100003",
                    Street = "88 Main Street"
                },
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "100004",
                    Warehouse = "1600"
                },
                new GetCustomersPhoneNumberDataDto
                {
                    SapId = "100005",
                    Contacts =
                    [
                        new ContactDto { Name = "Amy Chen", Identity = "Manager" }
                    ]
                }
            ]);

        _sjFoodClient.GetCustomerAiQuotationAsync(Arg.Any<GetCustomerAiQuotationRequestDto>(), cancellationToken)
            .Returns(new GetCustomerAiQuotationResponseDto
            {
                AiQuotationList = [new AiQuotationDto { SjAiCost = 12.5 }]
            });

        var result = await sut.QueryPriceByPhoneAndProductAsync("+19164284295", "beef", new SjFoodCustomerMatchHints
        {
            CustomerHint = customerHint,
            CustomerName = customerNameHint,
            HeaderNote1 = headerNoteHint,
            Street = streetHint,
            Warehouse = warehouseHint,
            ContactIdentity = contactIdentityHint,
            ContactName = contactNameHint
        }, cancellationToken);

        result.SapId.ShouldBe(expectedSapId);
        result.Message.ShouldContain("SJ价 12.5");
        await _sjFoodClient.Received(1).GetCustomerAiQuotationAsync(
            Arg.Is<GetCustomerAiQuotationRequestDto>(x => x.CustomerId == expectedSapId && x.ProductNameList.SequenceEqual(new[] { "beef" })),
            cancellationToken);
    }

    // SAPID 是最强识别信息。即使 AI 传入不带前导 0 的 ID，也应能匹配 CRM 返回的完整 SAPID。
    [Fact]
    public async Task QueryPriceByPhoneAndProductAsync_ShouldDisambiguateBySapIdIgnoringLeadingZeros()
    {
        var sut = new SjFoodQuotationService(_crmClient, _sjFoodClient);
        var cancellationToken = CancellationToken.None;

        _crmClient.GetCrmTokenAsync(cancellationToken).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(Arg.Any<GetCustmoersByPhoneNumberRequestDto>(), "crm-token", cancellationToken)
            .Returns([
                new GetCustomersPhoneNumberDataDto { SapId = "000123" },
                new GetCustomersPhoneNumberDataDto { SapId = "000456" }
            ]);

        _sjFoodClient.GetCustomerAiQuotationAsync(Arg.Any<GetCustomerAiQuotationRequestDto>(), cancellationToken)
            .Returns(new GetCustomerAiQuotationResponseDto
            {
                AiQuotationList = [new AiQuotationDto { KfOrOsAiCost = 9.75 }]
            });

        var result = await sut.QueryPriceByPhoneAndProductAsync("+19164284295", "noodle", new SjFoodCustomerMatchHints
        {
            SapId = "123"
        }, cancellationToken);

        result.SapId.ShouldBe("000123");
        result.Message.ShouldContain("KF/OS价 9.75");
        await _sjFoodClient.Received(1).GetCustomerAiQuotationAsync(
            Arg.Is<GetCustomerAiQuotationRequestDto>(x => x.CustomerId == "000123" && x.ProductNameList.SequenceEqual(new[] { "noodle" })),
            cancellationToken);
    }

    // 下列场景都不能唯一确定客户，因此必须停止报价查询：
    // 1. 一个 hint 命中多个客户；2. 多个客户但没有任何 hint；
    // 3. CRM 没有返回有效 SAPID；4. 产品名不能被当作客户身份线索。
    [Theory]
    [MemberData(nameof(UnresolvedCustomerCasesAsObjectArray))]
    public async Task QueryPriceByPhoneAndProductAsync_ShouldNotCallQuotation_WhenCustomerCannotBeResolved(
        List<GetCustomersPhoneNumberDataDto> customers,
        SjFoodCustomerMatchHints hints,
        string productName)
    {
        var sut = new SjFoodQuotationService(_crmClient, _sjFoodClient);
        var cancellationToken = CancellationToken.None;

        _crmClient.GetCrmTokenAsync(cancellationToken).Returns("crm-token");
        _crmClient.GetCustomersByPhoneNumberAsync(Arg.Any<GetCustmoersByPhoneNumberRequestDto>(), "crm-token", cancellationToken)
            .Returns(customers);

        var result = await sut.QueryPriceByPhoneAndProductAsync("+19164284295", productName, hints, cancellationToken);

        result.SapId.ShouldBeNull();
        result.Message.ShouldNotBeNullOrWhiteSpace();
        await _sjFoodClient.DidNotReceiveWithAnyArgs().GetCustomerAiQuotationAsync(default, default);
    }

    public static IEnumerable<object[]> UnresolvedCustomerCasesAsObjectArray() =>
        UnresolvedCustomerCases().Select(x => new object[] { x.Customers, x.Hints, x.ProductName });

    private static IEnumerable<(List<GetCustomersPhoneNumberDataDto> Customers, SjFoodCustomerMatchHints Hints, string ProductName)> UnresolvedCustomerCases()
    {
        yield return
        (
            new List<GetCustomersPhoneNumberDataDto>
            {
                new() { SapId = "100001", CustomerName = "Alice Chen" },
                new() { SapId = "100002", CustomerName = "Alice Zhang" }
            },
            new SjFoodCustomerMatchHints { CustomerName = "Alice" },
            "beef"
        );

        yield return
        (
            new List<GetCustomersPhoneNumberDataDto>
            {
                new() { SapId = "100001", CustomerName = "Alice Chen" },
                new() { SapId = "100002", CustomerName = "Bob Zhang" }
            },
            null,
            "beef"
        )!;

        yield return
        (
            new List<GetCustomersPhoneNumberDataDto>
            {
                new() { SapId = null, CustomerName = "Alice Chen" },
                new() { SapId = "", CustomerName = "Bob Zhang" }
            },
            new SjFoodCustomerMatchHints { CustomerName = "Alice Chen" },
            "beef"
        );

        yield return
        (
            new List<GetCustomersPhoneNumberDataDto>
            {
                new() { SapId = "100001", HeaderNote1 = "Beef House" },
                new() { SapId = "100002", CustomerName = "Bob Zhang" }
            },
            null,
            "Beef"
        )!;
    }
}
