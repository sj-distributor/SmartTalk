using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Printer;

namespace SmartTalk.Messages.Events.Printer;

public class PrinterJobConfirmedEvent : IEvent
{
    public MerchPrinterOrderDto MerchPrinterOrderDto { get; set; }

    public string PrinterMac { get; set; }

    public string PrinterStatusCode { get; set; }

    public bool IsPrintError()
    {
        var code = GetCode();
        return code != null && !code.ToString().StartsWith("2");
    }

    public int? GetCode()
    {
        if (string.IsNullOrWhiteSpace(PrinterStatusCode))
            return null;

        if (int.TryParse(PrinterStatusCode[0].ToString(), out int _))
        {
            if (int.TryParse(PrinterStatusCode.Split(' ').FirstOrDefault(), out int value))
                return value;
        }

        return null;
    }

    public string GetCodeDescription()
    {
        var code = GetCode();
        if (code != null)
            return string.Join(' ',PrinterStatusCode.Split(' ').Skip(1));

        return null;
    }
}