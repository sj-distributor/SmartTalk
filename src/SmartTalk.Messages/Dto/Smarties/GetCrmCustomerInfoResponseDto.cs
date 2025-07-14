using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Smarties;

public class GetCrmCustomerInfoResponseDto
{
    [JsonProperty("data")]
    public List<CrmCustomerInfoDto> Data { get; set; }

    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("msg")]
    public string Msg { get; set; }
}

public class CrmCustomerInfoDto
{
    [JsonProperty("customer_id")]
    public string CustomerId { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("address")]
    public string Address { get; set; }

    [JsonProperty("products")]
    public List<CrmProductDto> Products { get; set; }
}

public class CrmProductDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("attributes")]
    public List<CrmProductAttributeDto> Attributes { get; set; }
}

public class CrmProductAttributeDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("options")]
    public List<CrmProductOptionDto> Options { get; set; }
}

public class CrmProductOptionDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set;}
}