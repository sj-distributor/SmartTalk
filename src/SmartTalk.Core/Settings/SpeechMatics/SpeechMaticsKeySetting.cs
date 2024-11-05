using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.SpeechMatics;

public class SpeechMaticsKeySetting : IConfigurationSetting
{
    public SpeechMaticsKeySetting(IConfiguration configuration)
    {
        SpeechMaticsKeyEarlyWarningRobotUrl = configuration.GetValue<string>("SpeechMaticsKeyEarlyWarningRobotUrl");
    }

    public string SpeechMaticsKeyEarlyWarningRobotUrl { get; set; }
}