using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Twilio;

public class TwilioSettings
{
    public TwilioSettings(IConfiguration configuration)
    {
        AuthToken = configuration.GetValue<string>("Twilio:AuthToken");
        AccountSid = configuration.GetValue<string>("Twilio:AccountSid");
    }
    
    public string AuthToken { get; set; } 
    
    public string AccountSid { get; set; }
}