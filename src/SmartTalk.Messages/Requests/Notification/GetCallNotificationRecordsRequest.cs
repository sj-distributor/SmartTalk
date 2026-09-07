using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Notification;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Notification;

public class GetCallNotificationRecordsRequest: IRequest
{
    public int? CompanyId { get; set; }
    
    public int? StoreId { get; set; }
    
    public string ScenarioKey { get; set; }
    
    public CallNotificationStatus? Status { get; set; }
    
    public string CustomerNumberKeyword { get; set; }
    
    public int PageIndex { get; set; } = 1;
    
    public int PageSize { get; set; } = 50;
}

public class CallNotificationRecordsResponse : SmartTalkResponse<CallNotificationRecordsResponseData>
{
}

public class CallNotificationRecordsResponseData
{
    public int Count { get; set; }
    public List<CallNotificationRecordDto> Records { get; set; } = [];
}

public class CallNotificationRecordDto
{
    public int Id { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int CompanyId { get; set; }
    
    public int StoreId { get; set; }
    
    public string ScenarioKey { get; set; }
    
    public string CustomerNumber { get; set; }
    
    public string Channel { get; set; }
    
    public CallNotificationStatus Status { get; set; }
    
    public string Content { get; set; }
    
    public string FailureReason { get; set; }
    
    public int RetryCount { get; set; }
}

