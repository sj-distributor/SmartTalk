using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class CopyAutoTestDataSetCommand : ICommand
{
    public List<int> ItemIds { get; set; }
    
    public int SourceDataSetId { get; set; }
    
    public int TargetDataSetId { get; set; }
}

public class CopyAutoTestDataSetResponse : SmartTalkResponse
{
}
