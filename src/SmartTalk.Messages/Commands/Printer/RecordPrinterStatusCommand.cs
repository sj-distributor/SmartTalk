using Mediator.Net.Contracts;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Messages.Commands.Printer;

public class RecordPrinterStatusCommand : PrinterStatusInfo,ICommand
{
    public string PrinterMac { get; set; }

    public Guid Token { get; set; }
}