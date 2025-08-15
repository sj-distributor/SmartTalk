using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneCallRecordDetailDto
{
    [JsonProperty("知識庫")]
    public string Name { get; set; }
    
    [JsonProperty("錄音")]
    public string Url { get; set; }
    
    [JsonProperty("時長")]
    public double? Duration { get; set; }
    
    [JsonProperty("來電號碼")]
    public string PhoneNumber { get; set; }
    
    [JsonProperty("來電類型")]
    public string InBoundType { get; set; }
    
    [JsonProperty("來電時間")]
    public string CreatedDate { get; set; }
    
    [JsonProperty("是否轉接人工")]
    public string IsTransfer { get; set; }
}