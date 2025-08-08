using Mediator.Net.Contracts;
using Newtonsoft.Json;
using SmartTalk.Messages.Dto.Printer;

namespace SmartTalk.Messages.Commands.Printer;

public class PrinterJobCommand : ICommand
{
    [JsonProperty("mac")]
    public string PrinterMac { get; set; }
        
    [JsonProperty("token")]
    public Guid JobToken { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
}

public class PrinterJobResponse : IResponse
{
    public MerchPrinterOrderDto MerchPrinterOrder { get; set; }
}