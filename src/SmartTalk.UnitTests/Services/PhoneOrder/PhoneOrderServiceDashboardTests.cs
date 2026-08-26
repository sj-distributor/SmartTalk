using AutoMapper;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Pos;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderServiceDashboardTests
{
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly IPosDataProvider _posDataProvider = Substitute.For<IPosDataProvider>();
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider = Substitute.For<IPhoneOrderDataProvider>();

    [Fact]
    public async Task GetPhoneOrderDataDashboardAsync_ShouldUseDashboardProjectionQueries()
    {
        var sut = PhoneOrderServiceTestFactory.Create(_mapper, _posDataProvider, _phoneOrderDataProvider);
        var start = new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.FromHours(8));
        var end = new DateTimeOffset(2026, 8, 25, 23, 59, 59, TimeSpan.FromHours(8));
        var command = new GetPhoneOrderDataDashboardCommand
        {
            StartDate = start,
            EndDate = end,
            AgentIds = [2331, 2495],
            StoreIds = [311, 268],
            DataType = PhoneOrderDataDashDataType.Data
        };

        _phoneOrderDataProvider
            .GetPhoneOrderDashboardRecordsAsync(
                Arg.Any<List<int>>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                [
                    new PhoneOrderDashboardRecordProjection
                    {
                        AgentId = 2331,
                        Status = PhoneOrderRecordStatus.Sent,
                        CreatedDate = start.AddHours(1),
                        PhoneNumber = "+10000000001",
                        Duration = 120,
                        OrderRecordType = PhoneOrderRecordType.InBound,
                        IsCustomerFriendly = true,
                        Scenario = DialogueScenarios.Order
                    }
                ],
                [
                    new PhoneOrderDashboardRecordProjection
                    {
                        AgentId = 2495,
                        Status = PhoneOrderRecordStatus.Sent,
                        CreatedDate = start.AddDays(-1),
                        PhoneNumber = "+10000000002",
                        Duration = 60,
                        OrderRecordType = PhoneOrderRecordType.InBound,
                        IsCustomerFriendly = true,
                        Scenario = DialogueScenarios.Inquiry
                    },
                    new PhoneOrderDashboardRecordProjection
                    {
                        AgentId = 2495,
                        Status = PhoneOrderRecordStatus.Recieved,
                        CreatedDate = start.AddDays(-1).AddHours(1),
                        PhoneNumber = "+10000000003",
                        Duration = 20,
                        OrderRecordType = PhoneOrderRecordType.OutBount
                    }
                ]);

        _posDataProvider
            .GetPosOrderDashboardProjectionsAsync(
                Arg.Any<List<int>>(),
                Arg.Any<bool?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                [
                    new PosOrderDashboardProjection
                    {
                        StoreId = 311,
                        CreatedDate = start.AddHours(2),
                        ModifiedStatus = PosOrderModifiedStatus.Normal,
                        Total = 20
                    },
                    new PosOrderDashboardProjection
                    {
                        StoreId = 311,
                        CreatedDate = start.AddHours(3),
                        ModifiedStatus = PosOrderModifiedStatus.Cancelled,
                        Total = 5
                    }
                ],
                [
                    new PosOrderDashboardProjection
                    {
                        StoreId = 268,
                        CreatedDate = start.AddDays(-1),
                        ModifiedStatus = PosOrderModifiedStatus.Normal,
                        Total = 10
                    }
                ]);

        var response = await sut.GetPhoneOrderDataDashboardAsync(command, CancellationToken.None);

        response.Data.CallInData.AnsweredCallInCount.ShouldBe(1);
        response.Data.CallInData.CountChange.ShouldBe(0);
        response.Data.CallOutData.CountChange.ShouldBe(-1);
        response.Data.Restaurant.OrderCount.ShouldBe(2);
        response.Data.Restaurant.CancelledOrderCount.ShouldBe(1);
        response.Data.Restaurant.TotalOrderAmount.ShouldBe(20);
        response.Data.Restaurant.OrderCountChange.ShouldBe(1);

        await _phoneOrderDataProvider.Received(2).GetPhoneOrderDashboardRecordsAsync(
            Arg.Is<List<int>>(x => x.SequenceEqual(command.AgentIds)),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>());
        await _phoneOrderDataProvider.DidNotReceive().GetPhoneOrderRecordsAsync(
            Arg.Any<List<int>>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<List<DialogueScenarios>>(),
            Arg.Any<int?>(),
            Arg.Any<List<string>>(),
            Arg.Any<CancellationToken>());
        await _posDataProvider.Received(2).GetPosOrderDashboardProjectionsAsync(
            Arg.Is<List<int>>(x => x.SequenceEqual(command.StoreIds)),
            true,
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>());
    }
}
