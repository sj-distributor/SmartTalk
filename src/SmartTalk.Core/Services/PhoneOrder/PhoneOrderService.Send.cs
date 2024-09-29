using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task DailyDataBroadcastAsync(SchedulingPhoneOrderDailyDataBroadcastCommand command, CancellationToken cancellationToken);   
}

public partial class PhoneOrderService : IPhoneOrderService
{
    public async Task DailyDataBroadcastAsync(SchedulingPhoneOrderDailyDataBroadcastCommand command, CancellationToken cancellationToken)
    {
       // todo 前一天八点到下午四点的录音数量(夜班)和下午四点到次日一点的(白班)
       
       
       //todo 每个用户的record标记量从当月20号到次月19号
    }
}