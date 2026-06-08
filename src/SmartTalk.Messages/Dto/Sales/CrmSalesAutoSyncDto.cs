using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Sales;

public class CrmSalesAutoSyncCustomerDto
{
    [JsonProperty("sap_id")]
    public string CustomerId { get; set; }

    [JsonProperty("customer_name")]
    public string CustomerName { get; set; }

    [JsonProperty("sales_name")]
    public string SalesName { get; set; }

    [JsonProperty("sales_group")]
    public string SalesGroup { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; }
}

public class CrmSalesAutoSyncPagedResponseDto
{
    [JsonProperty("current_page")]
    public int CurrentPage { get; set; }

    [JsonProperty("per_page")]
    public int PerPage { get; set; }

    [JsonProperty("last_page")]
    public int LastPage { get; set; }

    [JsonProperty("total")]
    public int Total { get; set; }

    [JsonProperty("data")]
    public List<CrmSalesAutoSyncCustomerDto> Data { get; set; } = new();
}
