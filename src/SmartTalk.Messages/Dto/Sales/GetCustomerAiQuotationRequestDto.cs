using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Sales;

public class GetCustomerAiQuotationRequestDto
{
    [JsonProperty("customerId")]
    public string CustomerId { get; set; }

    [JsonProperty("materialIdList")]
    public List<string> MaterialIdList { get; set; } = [];
}

public class GetCustomerAiQuotationResponseDto
{
    [JsonProperty("aiQuotationList")]
    public List<AiQuotationDto> AiQuotationList { get; set; } = [];

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("code")]
    public int Code { get; set; }
}

public class AiQuotationDto
{
    [JsonProperty("customerId")]
    public string CustomerId { get; set; }

    [JsonProperty("queryDate")]
    public DateTime QueryDate { get; set; }

    [JsonProperty("sjAiCost")]
    public double? SjAiCost { get; set; }

    [JsonProperty("kfOrOsAiCost")]
    public double? KfOrOsAiCost { get; set; }

    [JsonProperty("materialId")]
    public string MaterialId { get; set; }
}
