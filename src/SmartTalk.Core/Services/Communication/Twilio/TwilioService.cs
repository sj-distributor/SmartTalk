using SmartTalk.Core.Ioc;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.WeChat;
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
    private readonly IWeChatClient _weChatClient;
    private readonly PhoneCallBroadcastSetting _phoneCallBroadcastSetting;
    
    public TwilioService(IWeChatClient weChatClient, PhoneCallBroadcastSetting phoneCallBroadcastSetting)
    {
        _weChatClient = weChatClient;
        _phoneCallBroadcastSetting = phoneCallBroadcastSetting;
    }

    public async Task HandlePhoneCallStatusCallbackAsync(HandlePhoneCallStatusCallBackCommand callback, CancellationToken cancellationToken)
    {
        if (_phoneCallBroadcastSetting.PhoneNumber != callback.From) return;

        TryParsePhoneCallStatus(callback.Status, out var result);
        
        if (result != PhoneCallStatus.Completed)
            await _weChatClient.SendWorkWechatRobotMessagesAsync(_phoneCallBroadcastSetting.BroadcastUrl, new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = new SendWorkWechatGroupRobotTextDto
                {
                    Content = $"PhoneCall Number: {result.GetDescription()},\n    Status: {result.GetDescription()}"
                }
            }, cancellationToken).ConfigureAwait(false);
    }
    
    public static bool TryParsePhoneCallStatus(string status, out PhoneCallStatus result)
    {
        if (Enum.TryParse(status, true, out PhoneCallStatus recordStatus))
        {
            result = recordStatus;
            return true;
        }

        var match = Enum.GetValues(typeof(PhoneCallStatus))
            .Cast<PhoneCallStatus>()
            .FirstOrDefault(x => x.GetDescription().Equals(status, StringComparison.OrdinalIgnoreCase));

        if (match != default)
        {
            result = match;
            return true;
        }

        result = default;
        return false;
    }
}