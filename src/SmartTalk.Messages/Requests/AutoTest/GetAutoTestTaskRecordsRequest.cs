using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AutoTest;

public class GetAutoTestTaskRecordsRequest : IRequest
{
    public int TaskId { get; set; }
    
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
}

public class GetAutoTestTaskRecordsResponse : SmartTalkResponse<GetAutoTestTaskRecordsResponseData>
{
}

public class GetAutoTestTaskRecordsResponseData
{
    public int Count { get; set; }
    
    public AutoTestTaskInfoDto TaskInfo { get; set; }
    
    public List<AutoTestTaskRecordDto> TaskRecords { get; set; }
}