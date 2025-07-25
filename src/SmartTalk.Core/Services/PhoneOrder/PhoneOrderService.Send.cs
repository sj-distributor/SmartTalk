﻿using System.Text;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Enums.WeChat;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task DailyDataBroadcastAsync(SchedulingPhoneOrderDailyDataBroadcastCommand command, CancellationToken cancellationToken);
    
    Task SendWorkWeChatRobotNotifyAsync(byte[] recordContent, string robotUrl, string transcription, string[] mentionedList, CancellationToken cancellationToken);
}

public partial class PhoneOrderService : IPhoneOrderService
{
    public async Task DailyDataBroadcastAsync(SchedulingPhoneOrderDailyDataBroadcastCommand command, CancellationToken cancellationToken)
    {
        var (today, yesterday) = PstTime();

        var dailyDataReport = await GenerateDailyDataReportAsync(today, yesterday, cancellationToken).ConfigureAwait(false);
        var assessmentPeriodReport = await GenerateCustomerServiceAssessmentPeriodReportAsync(today, yesterday, cancellationToken).ConfigureAwait(false);

        foreach (var robotUrl in command.RobotUrl)
        {
            await _weChatClient.SendWorkWechatRobotMessagesAsync(robotUrl,
                new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = new SendWorkWechatGroupRobotTextDto
                {
                    Content = $"SMARTTALK AI每日數據播報:\n{yesterday:MM/dd/yyyy}\n1.平台錄音數量\n{dailyDataReport}\n2.AI素材校準量\n{assessmentPeriodReport}"
                }
            }, cancellationToken).ConfigureAwait(false);
        }
    }
    
    public async Task SendWorkWeChatRobotNotifyAsync(byte[] recordContent, string key, string transcription, string[] mentionedList, CancellationToken cancellationToken)
    {
        var robotUrl = $"https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key={key}";
        
        // await _weChatClient.SendWorkWechatRobotMessagesAsync(robotUrl,
        //     new SendWorkWechatGroupRobotMessageDto
        //     {
        //         MsgType = "text",
        //         Text = new SendWorkWechatGroupRobotTextDto
        //         {
        //             Content = $"-------------------------Start-------------------------"
        //         }
        //     }, cancellationToken);
        
        // var splitAudios = await ConvertAndSplitAudioAsync(recordContent, secondsPerAudio: 60, cancellationToken: cancellationToken).ConfigureAwait(false);

        // await SendMultiAudioMessagesAsync(splitAudios, key, cancellationToken).ConfigureAwait(false);

        await _weChatClient.SendWorkWechatRobotMessagesAsync(
            robotUrl, new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text", Text = new SendWorkWechatGroupRobotTextDto { Content = transcription, MentionedList = mentionedList }
            }, CancellationToken.None);
        
        // await _weChatClient.SendWorkWechatRobotMessagesAsync(
        //     robotUrl, new SendWorkWechatGroupRobotMessageDto
        //     {
        //         MsgType = "text", Text = new SendWorkWechatGroupRobotTextDto { Content = "-------------------------End-------------------------" }
        //     }, CancellationToken.None);
    }

    private async Task<List<byte[]>> ConvertAndSplitAudioAsync(byte[] record, int secondsPerAudio, CancellationToken cancellationToken)
    {
        var amrAudio = await _ffmpegService.ConvertWavToAmrAsync(record, "", cancellationToken: cancellationToken).ConfigureAwait(false);

        return await _ffmpegService.SplitAudioAsync(amrAudio, secondsPerAudio, "amr", cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    private async Task SendMultiAudioMessagesAsync(List<byte[]> audios, string key, CancellationToken cancellationToken)
    {
        foreach (var audio in audios)
        {
            var uploadResponse = await _weChatClient.UploadWorkWechatTemporaryFileAsync(
                $"https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media?key={key}&type=voice", Guid.NewGuid() + ".amr", UploadWorkWechatTemporaryFileType.Voice, audio, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(uploadResponse?.MediaId)) continue;
            
            await _weChatClient.SendWorkWechatRobotMessagesAsync($"https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key={key}",
                new SendWorkWechatGroupRobotMessageDto
                {
                    MsgType = "voice",
                    Voice = new SendWorkWechatGroupRobotFileDto { MediaId = uploadResponse.MediaId }
                }, cancellationToken);
        }
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
            stringBuilder.AppendLine($"{restaurant.Restaurant?.Name ?? string.Empty}:");
        
            var index = 1;
            
            foreach (var classe in restaurant.Classes)
            {
                stringBuilder.AppendLine($"   {index++}) {classe.TimeFrame}: {classe.Count}");
            }
        }

        return stringBuilder.ToString();
    }

    private async Task<string> GenerateCustomerServiceAssessmentPeriodReportAsync(DateTimeOffset today, DateTimeOffset yesterday, CancellationToken cancellationToken)
    {
        var (previous20Th, nowDate) = CustomerServiceAssessmentPeriod(today, yesterday);

        var (todayStartTime, todayEndTime) = CustomerServiceToday(today, yesterday);
        
        var userHasProofreadTheNumber = await _phoneOrderDataProvider.GetPhoneOrderRecordsWithUserCountAsync(
            previous20Th, nowDate, cancellationToken).ConfigureAwait(false);
        
        var userTodayHasProofreadTheNumber = await _phoneOrderDataProvider.GetPhoneOrderRecordsWithUserCountAsync(
            todayStartTime, todayEndTime, cancellationToken).ConfigureAwait(false);
        
        var stringBuilder = new StringBuilder();
        var userIndex = 1;

        foreach (var user in userHasProofreadTheNumber)
        {
            var todayCount = userTodayHasProofreadTheNumber.FirstOrDefault(x => x.UserName == user.UserName)?.Count ?? 0;
            
            stringBuilder.AppendLine($"   {userIndex++}) {user.UserName}: {todayCount} (當期累計: {user.Count})");
        }

        return stringBuilder.ToString();
    }
    
    private static (DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime) DefineTimeInterval(DateTimeOffset today, DateTimeOffset yesterday) => 
        (new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 15, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero));
    
    private static (DateTimeOffset startPeriod, DateTimeOffset endPeriod) CustomerServiceAssessmentPeriod(DateTimeOffset today, DateTimeOffset yesterday)
    {
        var startPeriod = today.Day >= 21
            ? new DateTimeOffset(today.Year, today.Month, 20, 16, 0, 0, TimeSpan.Zero) 
            : new DateTimeOffset(today.Year, today.Month, 20, 16, 0, 0, TimeSpan.Zero).AddMonths(-1);
        
        var endPeriod = new DateTimeOffset(today.Year, today.Month, today.Day, 16, 0, 0, TimeSpan.Zero);
        
        return (startPeriod, endPeriod);
    }

    private static (DateTimeOffset startPeriod, DateTimeOffset endPeriod) CustomerServiceToday(DateTimeOffset today, DateTimeOffset yesterday)
    {
        var startPeriod = new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 16, 0, 0, TimeSpan.Zero);

        var endPeriod = new DateTimeOffset(today.Year, today.Month, today.Day, 16, 0, 0, TimeSpan.Zero);

        return (startPeriod, endPeriod);
    }
}