using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Crm;

public class GetCustomerIdByShopNameResponseDto
{
    [JsonProperty("sap_id")]
    public string SapId { get; set; }

    [JsonProperty("restaurant_name_remark")]
    public string RestaurantNameRemark { get; set; }

    [JsonProperty("customer_name")]
    public string CustomerName { get; set; }

    [JsonProperty("restaurant_cn_name")]
    public string RestaurantCnName { get; set; }
}
