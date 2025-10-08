namespace SmartTalk.Messages.Dto.RingCentral;

public class RingCentralCallLogResponseDto
{
    public string Uri { get; set; }
    
    public List<RingCentralRecordDto> Records { get; set; }
    
    public RingCentralPagingDto Paging { get; set; }
    
    public RingCentralNavigationDto Navigation { get; set; }
}

public class RingCentralRecordDto
{
    public string Uri { get; set; }
    
    public string Id { get; set; }
    
    public string SessionId { get; set; }
    
    public DateTime StartTime { get; set; }
    
    public int Duration { get; set; }
    
    public int DurationMs { get; set; }
    
    public string Type { get; set; }
    
    public string InternalType { get; set; }
    
    public string Direction { get; set; }
    
    public string Action { get; set; }
    
    public string Result { get; set; }

    public RingCentralPartyDto To { get; set; }
    
    public RingCentralPartyDto From { get; set; }

    public RingCentralRecordingDto Recording { get; set; }
    
    public RingCentralExtensionDto Extension { get; set; }

    public string TelephonySessionId { get; set; }
    
    public string PartyId { get; set; }
}

public class RingCentralPartyDto
{
    public string Name { get; set; }
    
    public string PhoneNumber { get; set; }
    
    public string Location { get; set; }
    
    public string ExtensionId { get; set; }
}

public class RingCentralRecordingDto
{
    public string Uri { get; set; }
    
    public string Id { get; set; }
    
    public string Type { get; set; }
    
    public string ContentUri { get; set; }
}

public class RingCentralExtensionDto
{
    public string Uri { get; set; }
    
    public long Id { get; set; }
}

public class RingCentralPagingDto
{
    public int Page { get; set; }
    
    public int PerPage { get; set; }
    
    public int PageStart { get; set; }
    
    public int PageEnd { get; set; }
}

public class RingCentralNavigationDto
{
    public RingCentralPageLinkDto NextPage { get; set; }
    
    public RingCentralPageLinkDto FirstPage { get; set; }
}

public class RingCentralPageLinkDto
{
    public string Uri { get; set; }
}