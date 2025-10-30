using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AutoTest;

public class GetAutoTestTaskRequest : IRequest
{
    public int ScenarioId { get; set; }
    
    public string KeyWord { get; set; }
    
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
}

public class GetAutoTestTaskResponse : SmartTalkResponse<GetAutoTestTaskResponseDataDto>
{
}

public class GetAutoTestTaskResponseDataDto
{
    public List<AutoTestTaskDto> Tasks { get; set; }
    
    public int TotalCount { get; set; }
}

