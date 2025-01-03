using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.WeChat;
using System.Text.RegularExpressions;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Messages.Enums.Twilio;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.Communication.PhoneCall;

namespace SmartTalk.Core.Services.Communication.Twilio;

public interface ITwilioService : IScopedDependency
{
    Task HandlePhoneCallStatusCallbackAsync(HandlePhoneCallStatusCallBackCommand callback, CancellationToken cancellationToken);
}

public class TwilioService : ITwilioService
{
    private readonly IMapper _mapper;
    private readonly IAsteriskClient _asteriskClient;
    private readonly IWeChatClient _weChatClient;
    private readonly PhoneCallBroadcastSetting _phoneCallBroadcastSetting;
    private readonly ITwilioServiceDataProvider _twilioServiceDataProvider;
    
    public TwilioService(IMapper mapper, IAsteriskClient asteriskClient, IWeChatClient weChatClient, PhoneCallBroadcastSetting phoneCallBroadcastSetting, ITwilioServiceDataProvider twilioServiceDataProvider)
    {
        _mapper = mapper;
        _weChatClient = weChatClient;
        _asteriskClient = asteriskClient;
        _phoneCallBroadcastSetting = phoneCallBroadcastSetting;
        _twilioServiceDataProvider = twilioServiceDataProvider;
    }

    public async Task HandlePhoneCallStatusCallbackAsync(HandlePhoneCallStatusCallBackCommand callback, CancellationToken cancellationToken)
    {
        if (!string.Equals(callback.Status, "Completed", StringComparison.OrdinalIgnoreCase) || _phoneCallBroadcastSetting.PhoneNumber != callback.From) return;

        var originalData = await _asteriskClient.GetAsteriskCdrAsync(Regex.Replace(_phoneCallBroadcastSetting.PhoneNumber, @"^\+1", ""), cancellationToken).ConfigureAwait(false);

        Log.Information("AsteriskCdr OriginalData: {@originalData}", originalData);
        
        var oldData = await _twilioServiceDataProvider.GetAsteriskCdrAsync(createdDate: originalData.Data[0].CallDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information($"AsteriskCdr OldData: {oldData}", oldData);

        if (oldData != null) return;
        
        await _twilioServiceDataProvider.CreateAsteriskCdrAsync(_mapper.Map<AsteriskCdr>(originalData.Data[0]), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var callStatus = TryParsePhoneCallStatus(originalData.Data[0].Disposition);

        if (callStatus != PhoneCallStatus.Answered)
            await SendWorkWechatRobotMessagesAsync($"🆘🆘 異常", true, cancellationToken).ConfigureAwait(false);
    }
    
    private static PhoneCallStatus TryParsePhoneCallStatus(string disposition)
    {
        if (string.IsNullOrWhiteSpace(disposition))
            return PhoneCallStatus.Failed;
        
        return Enum.TryParse(disposition.Replace(" ", ""), true, out PhoneCallStatus status) ? status : PhoneCallStatus.Failed;
    }

    private async Task SendWorkWechatRobotMessagesAsync(string content, bool atAll, CancellationToken cancellationToken)
    {
        var pacificTime = DateTimeOffset.UtcNow.ConvertFromUtc(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentDate = pacificTime.ToString("yyyy-MM-dd");
        var currentTime = pacificTime.ToString("HH:mm");
        
        var text = new SendWorkWechatGroupRobotTextDto { Content = $"PST {currentDate} linphone服务器情况 \n\n{currentTime} {content}" };

        if (atAll)
            text.MentionedMobileList = "@all";
        
        await _weChatClient.SendWorkWechatRobotMessagesAsync(_phoneCallBroadcastSetting.BroadcastUrl, new SendWorkWechatGroupRobotMessageDto
        {
            MsgType = "text",
            Text = text
        }, cancellationToken).ConfigureAwait(false);
    }
}