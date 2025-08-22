using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Printer;

namespace SmartTalk.Messages.Requests.Printer
{
    public class GetMerchPrintersRequest: IRequest
    {
        public int StoreId { get; set; }
    }

    public class GetMerchPrintersResponse : IResponse
    {
        public List<MerchPrinterDto> Result { get; set; }
    }
}
