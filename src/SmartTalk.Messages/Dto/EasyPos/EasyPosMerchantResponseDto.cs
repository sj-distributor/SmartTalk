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
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("dayOfWeeks")]
    public List<int> DayOfWeeks { get; set; }

    [JsonProperty("startTime")]
    public StoreTimeSpanInfo StartTime { get; set; }

    [JsonProperty("endTime")]
    public StoreTimeSpanInfo EndTime { get; set; }
    
    [JsonProperty("duration")]
    public StoreTimeSpanInfo Duration { get; set; }
    
    [JsonProperty("updateAt")]
    public DateTimeOffset UpdateAt { get; set; }
    
    [JsonProperty("associatedId")]
    public int AssociatedId { get; set; }
    
    [JsonProperty("associatedType")]
    public int AssociatedType { get; set; }
}

public class StoreTimeSpanInfo
{
    [JsonProperty("ticks")]
    public long Ticks { get; set; }

    [JsonProperty("days")]
    public int Days { get; set; }

    [JsonProperty("hours")]
    public int Hours { get; set; }

    [JsonProperty("milliseconds")]
    public int Milliseconds { get; set; }

    [JsonProperty("microseconds")]
    public int Microseconds { get; set; }
    
    [JsonProperty("nanoseconds")]
    public int Nanoseconds { get; set; }

    [JsonProperty("minutes")]
    public int Minutes { get; set; }

    [JsonProperty("seconds")]
    public int Seconds { get; set; }

    [JsonProperty("totalDays")]
    public double TotalDays { get; set; }

    [JsonProperty("totalHours")]
    public double TotalHours { get; set; }

    [JsonProperty("totalMilliseconds")]
    public double TotalMilliseconds { get; set; }

    [JsonProperty("totalMicroseconds")]
    public double TotalMicroseconds { get; set; }

    [JsonProperty("totalNanoseconds")]
    public double TotalNanoseconds { get; set; }

    [JsonProperty("totalMinutes")]
    public double TotalMinutes { get; set; }

    [JsonProperty("totalSeconds")]
    public double TotalSeconds { get; set; }
}