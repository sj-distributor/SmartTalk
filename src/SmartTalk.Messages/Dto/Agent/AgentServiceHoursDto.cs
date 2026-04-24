using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Agent;

public class AgentServiceHoursDto
{
    [JsonProperty("day")]
    public int Day { get; set; }
    
    [JsonProperty("hours")]
    public List<HoursDto> Hours { get; set; }
    
    public DayOfWeek DayOfWeek => Enum.IsDefined(typeof(DayOfWeek), Day) ? (DayOfWeek)Day : throw new InvalidOperationException($"Invalid Day value: {Day}");
}

public class HoursDto
{
    [JsonProperty("start")]
    public TimeSpan Start { get; set; }
    
    [JsonProperty("end")]
    public TimeSpan End { get; set; }
}