using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Printer
{
    public class GetMerchPrintersRequest: IRequest
    {
        public int StoreId { get; set; }
    }

    public class GetMerchPrintersResponse : SmartTalkResponse<List<MerchPrinterDto>>
    {
    }
}
