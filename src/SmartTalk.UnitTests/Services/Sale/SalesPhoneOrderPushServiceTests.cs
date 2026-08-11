using NSubstitute;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Enums.Sales;
using Xunit;

namespace SmartTalk.UnitTests.Services.Sale;

public class SalesPhoneOrderPushServiceTests
{
    private readonly ISalesDataProvider _salesDataProvider = Substitute.For<ISalesDataProvider>();
    private readonly ISalesClient _salesClient = Substitute.For<ISalesClient>();
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider = Substitute.For<IPhoneOrderDataProvider>();
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

    [Fact]
    public async Task ExecutePhoneOrderPushTasksAsync_ShouldCallDeleteOnce_WhenTaskIsDeleteOrder()
    {
        var task = new PhoneOrderPushTask
        {
            Id = 1,
            RecordId = 2,
            TaskType = PhoneOrderPushTaskType.DeleteOrder,
            RequestJson = """
                          {"CustomerNumber":"12345","SoldToIds":"12345","DeliveryDate":"2026-05-14T00:00:00","AiAssistantId":1}
                          """
        };

        var sut = new SalesPhoneOrderPushService(_salesDataProvider, _salesClient, _phoneOrderDataProvider, _backgroundJobClient);

        _salesDataProvider.GetRecordPushTaskByRecordIdAsync(task.RecordId, Arg.Any<CancellationToken>()).Returns(task);
        _salesDataProvider.IsParentCompletedAsync(task.ParentRecordId, Arg.Any<CancellationToken>()).Returns(true);
        _salesDataProvider.HasPendingTasksByRecordIdAsync(task.RecordId, Arg.Any<CancellationToken>()).Returns(false);
        _salesClient.DeleteAiOrderAsync(Arg.Any<DeleteAiOrderRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteAiOrderResponseDto { Data = Guid.NewGuid() });

        await sut.ExecutePhoneOrderPushTasksAsync(task.RecordId, CancellationToken.None);

        await _salesClient.Received(1).DeleteAiOrderAsync(Arg.Any<DeleteAiOrderRequestDto>(), Arg.Any<CancellationToken>());
    }
}
