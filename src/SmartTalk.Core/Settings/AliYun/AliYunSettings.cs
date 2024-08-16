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
    }
    
    public string AccessKeyId { get; set; }
    public string AccessKeySecret { get; set; }
    
    public string OssEndpoint { get; set; }
    public string OssBucketName { get; set; }
}