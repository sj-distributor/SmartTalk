using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Printer
{
    public class GetPrinterJobAvailableRequest : IRequest
    {
        public string PrinterMac { get; set; }

        public Guid Token { get; set; }
    }

    public class GetPrinterJobAvailableResponse : SmartTalkResponse
    {
        public bool JobReady { get; set; }

        /// <summary>
        /// https://star-m.jp/products/s_print/CloudPRNTSDK/Documentation/en/developerguide/pollingserver/post_jsonresponse.html
        /// </summary>
        public Guid? JobToken { get; set; }
        
        public IEnumerable<string> MediaTypes { get; } = new List<string>() { "application/vnd.star.line","application/vnd.star.linematrix","application/vnd.star.starprnt","application/vnd.star.starprntcore","text/vnd.star.markup" };
    }
}