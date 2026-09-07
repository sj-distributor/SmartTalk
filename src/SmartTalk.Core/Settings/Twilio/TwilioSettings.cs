using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Twilio;

public class TwilioSettings : IConfigurationSetting
{
    public TwilioSettings(IConfiguration configuration)
    {
        AuthToken = configuration.GetValue<string>("Twilio:AuthToken");
        AccountSid = configuration.GetValue<string>("Twilio:AccountSid");
        IncomingMessageWebhookUrl = configuration.GetValue<string>("Twilio:IncomingMessageWebhookUrl");
    }
    
    public string AuthToken { get; set; } 
    
    public string AccountSid { get; set; }

    // This must exactly match the public URL configured in the Twilio console.
    public string IncomingMessageWebhookUrl { get; set; }
}
