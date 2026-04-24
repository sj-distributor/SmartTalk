using Serilog;
using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.WeChat;
using System.Text.RegularExpressions;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Core.Domain.SipServer;
using SmartTalk.Messages.Enums.Twilio;
using SmartTalk.Core.Services.SipServer;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Messages.Enums.SipServer;
using SmartTalk.Core.Services.Asterisk;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.Communication.PhoneCall;

namespace SmartTalk.Core.Services.Webhook;

public interface ITwilioWebhookService : IScopedDependency
{
    Task HandlePhoneCallStatusCallbackAsync(HandlePhoneCallStatusCallBackCommand callback, CancellationToken cancellationToken);
}

public class TwilioWebhookService : ITwilioWebhookService
{
    private readonly IMapper _mapper;
    private readonly IAlidnsClient _alidnsClient;
    private readonly IWeChatClient _weChatClient;
    private readonly IAsteriskClient _asteriskClient;
    private readonly ISipServerDataProvider _sipServerDataProvider;
    private readonly PhoneCallBroadcastSetting _phoneCallBroadcastSetting;
    private readonly IAsteriskDataProvider _asteriskDataProvider;

    public TwilioWebhookService(IMapper mapper, IAlidnsClient alidnsClient, IAsteriskClient asteriskClient, IWeChatClient weChatClient, ISipServerDataProvider sipServerDataProvider, PhoneCallBroadcastSetting phoneCallBroadcastSetting, IAsteriskDataProvider asteriskDataProvider)
    {
        _mapper = mapper;
        _weChatClient = weChatClient;
        _alidnsClient = alidnsClient;
        _asteriskClient = asteriskClient;
        _sipServerDataProvider = sipServerDataProvider;
        _phoneCallBroadcastSetting = phoneCallBroadcastSetting;
        _asteriskDataProvider = asteriskDataProvider;
    }

    public async Task HandlePhoneCallStatusCallbackAsync(HandlePhoneCallStatusCallBackCommand callback, CancellationToken cancellationToken)
    {
        if (!string.Equals(callback.Status, "Completed", StringComparison.OrdinalIgnoreCase)) return;

        var restaurantAsterisk = (await _asteriskDataProvider.GetRestaurantAsteriskAsync(callback.To, callback.From, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        Log.Information("RestaurantAsterisk is: {@restaurantAsterisk}", restaurantAsterisk);

        if (restaurantAsterisk == null) return;

        var originalData = await _asteriskClient.GetAsteriskCdrAsync(Regex.Replace(callback.From, @"^\+1", ""), restaurantAsterisk.CdrBaseUrl, cancellationToken).ConfigureAwait(false);

        Log.Information("AsteriskCdr OriginalData: {@originalData}", originalData);

        var oldData = await _asteriskDataProvider.GetAsteriskCdrAsync(createdDate: originalData.Data[0].CallDate, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information($"AsteriskCdr OldData: {oldData}", oldData);

        if (oldData != null) return;

        await _asteriskDataProvider.CreateAsteriskCdrAsync(_mapper.Map<AsteriskCdr>(originalData.Data[0]), cancellationToken: cancellationToken).ConfigureAwait(false);

        var callStatus = TryParsePhoneCallStatus(originalData.Data[0].Disposition);

        if (callStatus != PhoneCallStatus.Answered)
            await ProcessCallBackExceptionsAsync(restaurantAsterisk, cancellationToken).ConfigureAwait(false);

        if (callStatus == PhoneCallStatus.Answered && restaurantAsterisk.PhonePathStatus == PhonePathStatus.Exception)
        {
            await SendWorkWechatRobotMessagesAsync($"✅✅已恢復正常", true, cancellationToken).ConfigureAwait(false);

            await UpdateAllDomainStatusAsync(restaurantAsterisk, PhonePathStatus.Running, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessCallBackExceptionsAsync(RestaurantAsterisk restaurantAsterisk, CancellationToken cancellationToken)
    {
        if (restaurantAsterisk.PhonePathStatus != PhonePathStatus.Exception)
        {
            await SendWorkWechatRobotMessagesAsync($"🆘🆘異常", true, cancellationToken).ConfigureAwait(false);

            await UpdateAllDomainStatusAsync(restaurantAsterisk, PhonePathStatus.Exception, cancellationToken).ConfigureAwait(false);
        }

        var sipBackupServers = (await _sipServerDataProvider.GetAllSipHostServersAsync(restaurantAsterisk.HostId, [SipServerStatus.Pending, SipServerStatus.InProgress], cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        Log.Information("Phone CallBack serviceIp:{@serviceIp}", sipBackupServers);

        var inProgressIp = sipBackupServers?.BackupServers?.FirstOrDefault(x => x.Status == SipServerStatus.InProgress);

        var pendingIp = sipBackupServers?.BackupServers?.FirstOrDefault(x => x.Status == SipServerStatus.Pending);

        if (pendingIp == null)
            throw new Exception("Cannot find sip backup server");

        var updateDomainRecord = await _alidnsClient.UpdateDomainRecordAsync(restaurantAsterisk.DomainName, restaurantAsterisk.Endpoint, restaurantAsterisk.HostRecords, pendingIp.ServerIp, cancellationToken).ConfigureAwait(false);

        Log.Information("Phone CallBack updateDomainRecord: {@updateDomainRecord}", updateDomainRecord);

        pendingIp.Status = SipServerStatus.InProgress;

        var updateSipBackupServers = new List<SipBackupServer>{pendingIp};

        if (inProgressIp != null)
        {
            inProgressIp.Status = SipServerStatus.Completed;
            updateSipBackupServers.Add(inProgressIp);
        }

        await _sipServerDataProvider.UpdateSipBackupServersAsync(updateSipBackupServers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAllDomainStatusAsync(RestaurantAsterisk restaurantAsterisk, PhonePathStatus phonePathStatus, CancellationToken cancellationToken)
    {
        var restaurantAsterisks = await _asteriskDataProvider.GetRestaurantAsteriskAsync(hostRecords: restaurantAsterisk.HostRecords, domainName: restaurantAsterisk.DomainName, cancellationToken: cancellationToken).ConfigureAwait(false);

        restaurantAsterisks = restaurantAsterisks.Select(x =>
        {
            x.PhonePathStatus = phonePathStatus;

            return x;
        }).ToList();

        await _asteriskDataProvider.UpdateRestaurantAsterisksAsync(restaurantAsterisks, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static PhoneCallStatus TryParsePhoneCallStatus(string disposition)
    {
        if (string.IsNullOrEmpty(disposition))
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
