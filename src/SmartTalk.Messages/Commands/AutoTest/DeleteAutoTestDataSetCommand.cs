using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class DeleteAutoTestDataSetCommand : ICommand
{
    public int DataSetId { get; set; }
    
    public List<int> ItemsIds { get; set; }
}

public class DeleteAutoTestDataSetResponse : SmartTalkResponse
{
}