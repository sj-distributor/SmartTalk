using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Printer
{
    [SmartTalkAuthorize(Permissions = new []{ SecurityStore.Permissions.CanViewMerchPrinter})]
    public class GetMerchPrintersRequest: IRequest
    {
        public int StoreId { get; set; }
    }

    public class GetMerchPrintersResponse : SmartTalkResponse<List<MerchPrinterDto>>
    {
    }
}
