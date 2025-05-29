using System.Text.Json.Serialization;

public class EasyPosMerchantResponseDto
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; }

    [JsonPropertyName("data")]
    public StoreInfoData Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class StoreInfoData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("companyId")]
    public int CompanyId { get; set; }

    [JsonPropertyName("timePeriods")]
    public List<StoreTimePeriod> TimePeriods { get; set; }
}

public class StoreTimePeriod
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("dayOfWeeks")]
    public List<int> DayOfWeeks { get; set; }

    [JsonPropertyName("startTime")]
    public StoreTimeSpanInfo StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public StoreTimeSpanInfo EndTime { get; set; }
}

public class StoreTimeSpanInfo
{
    [JsonPropertyName("ticks")]
    public long Ticks { get; set; }

    [JsonPropertyName("days")]
    public int Days { get; set; }

    [JsonPropertyName("hours")]
    public int Hours { get; set; }

    [JsonPropertyName("milliseconds")]
    public int Milliseconds { get; set; }

    [JsonPropertyName("microseconds")]
    public int Microseconds { get; set; }
    
    [JsonPropertyName("nanoseconds")]
    public int Nanoseconds { get; set; }

    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }

    [JsonPropertyName("seconds")]
    public int Seconds { get; set; }

    [JsonPropertyName("totalDays")]
    public double TotalDays { get; set; }

    [JsonPropertyName("totalHours")]
    public double TotalHours { get; set; }

    [JsonPropertyName("totalMilliseconds")]
    public double TotalMilliseconds { get; set; }

    [JsonPropertyName("totalMicroseconds")]
    public double TotalMicroseconds { get; set; }

    [JsonPropertyName("totalNanoseconds")]
    public double TotalNanoseconds { get; set; }

    [JsonPropertyName("totalMinutes")]
    public double TotalMinutes { get; set; }

    [JsonPropertyName("totalSeconds")]
    public double TotalSeconds { get; set; }
}