using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Printer;

public class PrintTestCommand : ICommand
{
    public int StoreId { get; set; }

    public string PrinterMac { get; set; }
}

public class PrintTestResponse : SmartTalkResponse
{
}