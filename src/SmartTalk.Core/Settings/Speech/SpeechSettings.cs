using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Speech;

public class SpeechSettings : IConfigurationSetting
{
    public EchoAvatarSettings EchoAvatar { get; set; }
    public SugarTalkSettings SugarTalk { get; set; }
    public TranscriptSettings Transcript { get; set; }

    public SpeechSettings(IConfiguration configuration)
    {
        EchoAvatar = new EchoAvatarSettings
        {
            BaseUrl = configuration.GetValue<string>("Speech:EchoAvatar:BaseUrl"),
            Apikey = configuration.GetValue<string>("Speech:EchoAvatar:Apikey"),
            CallBackUrl = configuration.GetValue<string>("Speech:EchoAvatar:CallBackUrl"),
            CantonBaseUrl = configuration.GetValue<string>("Speech:EchoAvatar:CantonBaseUrl")
        };
        
        SugarTalk = new SugarTalkSettings
        {
            BaseUrl = configuration.GetValue<string>("Speech:SugarTalk:BaseUrl"),
            Apikey = configuration.GetValue<string>("Speech:SugarTalk:Apikey")
        };
        
        Transcript = new TranscriptSettings
        {
            BaseUrl = configuration.GetValue<string>("Speech:Transcript:BaseUrl"),
            ApiKey = configuration.GetValue<string>("Speech:Transcript:ApiKey")
        };
    }

    public class EchoAvatarSettings
    {
        public string BaseUrl { get; set; }
        public string Apikey { get; set; }
        public string CallBackUrl { get; set; }
        public string CantonBaseUrl { get; set; }
    }

    public class SugarTalkSettings
    {
        public string BaseUrl { get; set; }
        public string Apikey { get; set; }
    }

    public class TranscriptSettings
    {
        public string BaseUrl { get; set; }

        public string ApiKey { get; set; }
    }
}
