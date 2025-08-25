using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Core.Handlers.RequestHandlers.Printer
{
    public class GetMerchPrinterLogRequestHandler : IRequestHandler<GetMerchPrinterLogRequest,GetMerchPrinterLogResponse>
    {
        private readonly IPrinterService _printerService;

        public GetMerchPrinterLogRequestHandler(IPrinterService printerService)
        {
            _printerService = printerService;
        }
        
        public async Task<GetMerchPrinterLogResponse> Handle(IReceiveContext<GetMerchPrinterLogRequest> context, CancellationToken cancellationToken)
        {
            return await _printerService.GetMerchPrinterLog(context.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}