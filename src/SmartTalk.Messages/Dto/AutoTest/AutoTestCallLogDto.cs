using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.AutoTest;

public class GetCallRecordsDataDto : SmartTalkResponse
{
    public List<AutoTestCallLogDto> Data { get; set; }
}

public class AutoTestCallLogDto
{
    public string Id { get; set; }
    
    public string Direction { get; set; }
    
    public string From { get; set; }
    
    public string To { get; set; }
    
    public string ExtensionId { get; set; }

    public DateTime StartTime { get; set; }

    public string RecordingUrl { get; set; }

    public string CallId { get; set; }

    public byte Source { get; set; }
}