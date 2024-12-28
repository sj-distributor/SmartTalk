using AutoMapper;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Enums.Twilio;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.Communication.PhoneCall;
using SmartTalk.Messages.Requests.Twilio;

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
        if (_phoneCallBroadcastSetting.PhoneNumber != callback.From) return;
        
        //todo 添加获取服务器数据，添加到本系统中
        var cdrData = await _asteriskClient.GetAsteriskCdrAsync(_phoneCallBroadcastSetting.PhoneNumber, cancellationToken).ConfigureAwait(false);

        //todo 持久化
        await _twilioServiceDataProvider.CreateAsteriskCdrAsync(_mapper.Map<AsteriskCdr>(cdrData.Data), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        
        TryParsePhoneCallStatus(cdrData.Data, out var result);
        
        if (result != PhoneCallStatus.Completed)
            await _weChatClient.SendWorkWechatRobotMessagesAsync(_phoneCallBroadcastSetting.BroadcastUrl, new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = new SendWorkWechatGroupRobotTextDto
                {
                    Content = $"PhoneCall Number: {callback.To},\n Status: {result.GetDescription()}"
                }
            }, cancellationToken).ConfigureAwait(false);
    }
    
    public static bool TryParsePhoneCallStatus(GetAsteriskCdrData cdrData, out PhoneCallStatus result)
    {
        
        return false;
    }
}