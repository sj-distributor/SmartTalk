using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Messages.Dto.Printer
{
    public class MerchPrinterDto
    {
        public int Id { get; set; }

        public int StoreId { get; set; }

        public string PrinterName { get; set; }

        public string PrinterMac { get; set; }

        public bool IsEnabled { get; set; }
        
        public Guid? Token { get; set; }

        public PrinterStatusInfo? PrinterStatusInfo { get; set; }
    }
}
