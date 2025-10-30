using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AutoTest;

public class CopyAutoTestDataSetRequest : IRequest
{
    public int SourceDataSetId { get; set; }
    
    public int TargetDataSetId { get; set; }
}

public class CopyAutoTestDataSetResponse : SmartTalkResponse
{
}
