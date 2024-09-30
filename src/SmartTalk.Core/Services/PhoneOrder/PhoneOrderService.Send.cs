using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.WeChat;
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
        var utcNow = DateTimeOffset.UtcNow;
        
        var pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        
        var today = TimeZoneInfo.ConvertTime(utcNow, pstZone);

        var yesterday = today.AddDays(-1);
        
        var dayShiftTime =  new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 8, 0, 0, TimeSpan.Zero);
        
        var dateTime = new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 16, 0, 0, TimeSpan.Zero);
        
        var nightShiftTime = new DateTimeOffset(today.Year, today.Month, today.Day, 1, 30, 0, TimeSpan.Zero);
        
        var restaurantCount = await _phoneOrderDataProvider.GetPhoneOrderRecordsForRestaurantCountAsync(dayShiftTime, dateTime, nightShiftTime, cancellationToken).ConfigureAwait(false);
        
        var previous20Th = today.Day - 1 >= 20 ? new DateTimeOffset(today.Year, today.Month, 20, 0, 0, 0, TimeSpan.Zero) : new DateTimeOffset(today.Year, today.Month, 20, 20, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);
        
        var nowDate = new  DateTimeOffset(today.Year, today.Month, today.Day, 23, 59, 59, TimeSpan.Zero);
        
        var userHasProofreadTheNumber = await _phoneOrderDataProvider.GetPhoneOrderRecordsWithUserCountAsync(previous20Th, nowDate, cancellationToken).ConfigureAwait(false);
        
        var restaurantString = "";
        var userString = "";
        var userIndex = 1;
        
        foreach (var restaurant in restaurantCount)
        {
            var index = 1;
            
            restaurantString = restaurantString + $"{restaurant.Restaurant.GetDescription()}:\n";
            
            foreach (var classe in restaurant.Classes)
            {
                restaurantString = restaurantString + $"   {index}){classe.TimeFrame}:{classe.Count}\n";
                
                index++;
            }
        }

        foreach (var user in userHasProofreadTheNumber)
        {
            userString = $"   {userIndex}){user.UserName}:{user.Count}\n";
            
            userIndex++;
        }
        
        await _weChatClient.SendWorkWechatRobotMessagesAsync(command.RobotUrl, 
            new SendWorkWechatGroupRobotMessageDto
        {
            MsgType = "text",
            Text = new SendWorkWechatGroupRobotTextDto
            {
                Content = $"SMART TALK AI每日數據播報:\n\n1.平台錄音數量\n{restaurantString}\n2.AI素材校準量\n{userString}"
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}