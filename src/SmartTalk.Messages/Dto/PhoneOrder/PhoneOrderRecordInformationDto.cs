using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordInformationDto
{
    public PhoneOrderRestaurant Restaurant { get; set; }
    
    public DateTimeOffset OrderDate { get; set; }
    
    public string OrderNumber { get; set; }
    
    public string WorkWeChatRobotKey { get; set; }

    public string WorkWeChatRobotUrl => "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=" + WorkWeChatRobotKey;

    public string WorkWeChatRobotUploadVoiceUrl => "https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media?key=" + WorkWeChatRobotKey + "&type=voice";
}