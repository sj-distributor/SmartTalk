using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class AddAutoTestDataSetByQuoteCommand : ICommand
{
    public int DataSetId { get; set; }
}

public class AddAutoTestDataSetByQuoteResponse : SmartTalkResponse
{
}