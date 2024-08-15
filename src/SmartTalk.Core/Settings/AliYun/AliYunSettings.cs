using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.AliYun;

public class AliYunSettings : IConfigurationSetting
{
    public AliYunSettings(IConfiguration configuration)
    {
        AccessKeyId = configuration.GetValue<string>("AliYun:AccessKeyId");
        AccessKeySecret = configuration.GetValue<string>("AliYun:AccessKeySecret");
        
        OssEndpoint = configuration.GetValue<string>("AliYun:Oss:Endpoint");
        OssBucketName = configuration.GetValue<string>("AliYun:Oss:BucketName");
        
        SmsEndpoint = configuration.GetValue<string>("AliYun:Sms:Endpoint");
        SmsSignName = configuration.GetValue<string>("AliYun:Sms:SignName");
        SmsTemplateCodeForVerificationCode = configuration.GetValue<string>("AliYun:Sms:TemplateCode:VerificationCode");
    }
    
    public string AccessKeyId { get; set; }
    public string AccessKeySecret { get; set; }
    
    public string OssEndpoint { get; set; }
    public string OssBucketName { get; set; }
    
    public string SmsEndpoint { get; set; }
    public string SmsSignName { get; set; }
    public string SmsTemplateCodeForVerificationCode { get; set; }
}