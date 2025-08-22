using Mediator.Net.Contracts;

namespace SmartTalk.Message.Commands.Printer
{
    public class AddMerchPrinterCommand : ICommand
    {
        public string PrinterName { get; set; }

        public string PrinterMac { get; set; }

        public int StoreId { get; set; }
    }
}