using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.PhoneCall;

public class TranscriptionCallbackSetting : IConfigurationSetting
{
    public TranscriptionCallbackSetting(IConfiguration configuration)
    {
        Url = configuration.GetValue<string>("TranscriptionCallback:Url");
        AuthHeaders = configuration.GetValue<string>("TranscriptionCallback:AuthHeaders").Split(",").ToList();
    }
    
    public string Url { get; set; }
    
    public List<string> AuthHeaders { get; set; }
}