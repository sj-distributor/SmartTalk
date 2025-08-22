using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Printer;

public class PrintTestCommand : ICommand
{
    public int StoreId { get; set; }

    public string PrinterMac { get; set; }
}