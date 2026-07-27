using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Sales;

public class GetOrderInformationByCustomerIdRequestDto
{
    [JsonProperty("CustomerIds")]
    public List<string> CustomerIds { get; set; } = new();
}

public class GetOrderInformationByCustomerIdResponseDto
{
    [JsonProperty("data")]
    public List<GetOrderInformationByCustomerIdItemDto> Data { get; set; } = new();
}

public class GetOrderInformationByCustomerIdItemDto
{
    [JsonProperty("invNumber")]
    public string InvNumber { get; set; }

    [JsonProperty("invDate")]
    public DateTime? InvDate { get; set; }

    [JsonProperty("materialName")]
    public string MaterialName { get; set; }
}
