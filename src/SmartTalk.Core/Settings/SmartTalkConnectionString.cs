using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings;

public class SmartTalkConnectionString : IConfigurationSetting<string>
{
    public SmartTalkConnectionString(IConfiguration configuration)
    {
        Value = configuration.GetConnectionString("SmartTalkConnectionString");
    }

    public string Value { get; set; }
}