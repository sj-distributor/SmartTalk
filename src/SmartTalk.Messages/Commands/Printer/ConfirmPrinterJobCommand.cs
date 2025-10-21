using Mediator.Net.Contracts;
using Newtonsoft.Json;

namespace SmartTalk.Messages.Commands.Printer;

public class ConfirmPrinterJobCommand : ICommand
{
    [JsonProperty("mac")]
    public string PrinterMac { get; set; }

    [JsonProperty("code")]
    public string PrintStatusCode { get; set; }
        
    [JsonProperty("token")]
    public Guid JobToken { get; set; }

    public bool PrintSuccessfully => !string.IsNullOrWhiteSpace(PrintStatusCode) && PrintStatusCode.StartsWith("2");
}