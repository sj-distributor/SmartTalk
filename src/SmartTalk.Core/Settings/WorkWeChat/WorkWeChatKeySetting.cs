using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.WorkWeChat;

public class WorkWeChatKeySetting : IConfigurationSetting
{
    public WorkWeChatKeySetting(IConfiguration configuration)
    {
        Key = configuration.GetValue<string>("WorkWeChat:Key");
    }
    
    public string Key { get; set; }
}