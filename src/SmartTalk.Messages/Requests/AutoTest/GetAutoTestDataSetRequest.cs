using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AutoTest;

public class GetAutoTestDataSetRequest : IRequest
{
    public int? Page { get; set; }
    
    public int? PageSize { get; set; }
    
    public string? KeyName { get; set; }
}

public class GetAutoTestDataSetResponse : SmartTalkResponse
{
   public GetAutoTestDataSetData Data { get; set; }

}

public class GetAutoTestDataSetData
{
    public int Count { get; set; }
    
    public List<AutoTestDataSetDto> Records { get; set; }
}
