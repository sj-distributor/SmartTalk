using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Core.Handlers.RequestHandlers.Printer
{
    public class GetMerchPrintersRequestHandler : IRequestHandler<GetMerchPrintersRequest, GetMerchPrintersResponse>
    {
        private readonly IPrinterService _printerService;

        public GetMerchPrintersRequestHandler(IPrinterService printerService)
        {
            _printerService = printerService;
        }

        public async Task<GetMerchPrintersResponse> Handle(IReceiveContext<GetMerchPrintersRequest> context, CancellationToken cancellationToken)
        {
            return await _printerService.GetMerchPrintersAsync(context.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}