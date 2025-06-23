using Newtonsoft.Json;

public class EasyPosMerchantResponseDto
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("msg")]
    public string Msg { get; set; }

    [JsonProperty("data")]
    public StoreInfoData Data { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }
}

public class StoreInfoData
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("companyId")]
    public long CompanyId { get; set; }
    
    [JsonProperty("shortName")]
    public string ShortName { get; set; }

    [JsonProperty("timePeriods")]
    public List<StoreTimePeriod> TimePeriods { get; set; }
    
    [JsonProperty("timezoneId")]
    public string TimeZoneId { get; set; }
}

public class StoreTimePeriod
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("dayOfWeeks")]
    public List<int> DayOfWeeks { get; set; }

    [JsonProperty("startTime")]
    public string StartTime { get; set; }

    [JsonProperty("endTime")]
    public string EndTime { get; set; }
    
    [JsonProperty("duration")]
    public string Duration { get; set; }
    
    [JsonProperty("updateAt")]
    public DateTimeOffset UpdateAt { get; set; }
    
    [JsonProperty("associatedId")]
    public int AssociatedId { get; set; }
    
    [JsonProperty("associatedType")]
    public int AssociatedType { get; set; }
}