using System.Text;
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
        var (today, yesterday) = PstTime();

        var dailyDataReport = await GenerateDailyDataReportAsync(today, yesterday, cancellationToken).ConfigureAwait(false);
        var assessmentPeriodReport = await GenerateCustomerServiceAssessmentPeriodReportAsync(today, cancellationToken).ConfigureAwait(false);

        await _weChatClient.SendWorkWechatRobotMessagesAsync(command.RobotUrl, 
            new SendWorkWechatGroupRobotMessageDto
        {
            MsgType = "text",
            Text = new SendWorkWechatGroupRobotTextDto
            {
                Content = $"SMART TALK AI每日數據播報:\n{yesterday:MM/dd/yyyy}\n1.平台錄音數量\n{dailyDataReport}\n2.AI素材校準量\n{assessmentPeriodReport}"
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static (DateTimeOffset today, DateTimeOffset yesterday) PstTime()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        var today = TimeZoneInfo.ConvertTime(utcNow, pstZone);
        var yesterday = today.AddDays(-1);

        return (today, yesterday);
    }
    
    private async Task<string> GenerateDailyDataReportAsync(DateTimeOffset today, DateTimeOffset yesterday, CancellationToken cancellationToken)
    {
        var (dayShiftTime, nightShiftTime, endTime) = DefineTimeInterval(today, yesterday);
    
        var restaurantCount = await _phoneOrderDataProvider
            .GetPhoneOrderRecordsForRestaurantCountAsync(dayShiftTime, nightShiftTime, endTime, cancellationToken).ConfigureAwait(false);
    
        var stringBuilder = new StringBuilder();
    
        foreach (var restaurant in restaurantCount)
        {
            stringBuilder.AppendLine($"{restaurant.Restaurant.GetDescription()}:");
        
            var index = 1;
            
            foreach (var classe in restaurant.Classes)
            {
                stringBuilder.AppendLine($"   {index++}) {classe.TimeFrame}: {classe.Count}");
            }
        }

        return stringBuilder.ToString();
    }

    private async Task<string> GenerateCustomerServiceAssessmentPeriodReportAsync(DateTimeOffset today, CancellationToken cancellationToken)
    {
        var (previous20Th, nowDate) = CustomerServiceAssessmentPeriod(today);
    
        var userHasProofreadTheNumber = await _phoneOrderDataProvider
            .GetPhoneOrderRecordsWithUserCountAsync(previous20Th, nowDate, cancellationToken)
            .ConfigureAwait(false);
    
        var stringBuilder = new StringBuilder();
        var userIndex = 1;

        foreach (var user in userHasProofreadTheNumber)
        {
            stringBuilder.AppendLine($"   {userIndex++}) {user.UserName}: {user.Count}");
        }

        return stringBuilder.ToString();
    }
    
    private static (DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime) DefineTimeInterval(DateTimeOffset today, DateTimeOffset yesterday) => 
        (new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 15, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero));
    
    private static (DateTimeOffset startPeriod, DateTimeOffset endPeriod) CustomerServiceAssessmentPeriod(DateTimeOffset today)
    {
        var startPeriod = today.Day >= 20 
            ? new DateTimeOffset(today.Year, today.Month, 20, 0, 0, 0, TimeSpan.Zero) 
            : new DateTimeOffset(today.Year, today.Month, 20, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);
        
        var endPeriod = new DateTimeOffset(today.Year, today.Month, today.Day, 23, 59, 59, TimeSpan.Zero);
        
        return (startPeriod, endPeriod);
    }
}