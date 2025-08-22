using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Printer;

public class DeleteMerchPrinterCommand : ICommand
{
    public int Id { get; set; }
}