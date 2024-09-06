using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Google;

public class GoogleTranslateApiKeySetting : IConfigurationSetting<string>
{
    public GoogleTranslateApiKeySetting(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("GoogleTranslateApiKey");
    }
    
    public string Value { get; set; }
}