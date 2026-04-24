using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Crm;

public class GetDeliveryInfoByPhoneNumberResponseDto
{
    [JsonProperty("sap_id")]
    public string SapId { get; set; }

    [JsonProperty("route_name")]
    public string RouteName { get; set; }

    [JsonProperty("delivery_time")]
    public string DeliveryTime { get; set; }

    [JsonProperty("entry_time")]
    public string EntryTime { get; set; }

    [JsonProperty("leave_time")]
    public string LeaveTime { get; set; }
}
