using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Core.Handlers.RequestHandlers.Printer;

public class GetPrinterJobAvailableRequestHandler : IRequestHandler<GetPrinterJobAvailableRequest, GetPrinterJobAvailableResponse>
{
    private readonly IPrinterService _printerService;

    public GetPrinterJobAvailableRequestHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    public async Task<GetPrinterJobAvailableResponse> Handle(IReceiveContext<GetPrinterJobAvailableRequest> context, CancellationToken cancellationToken)
    {
        return await _printerService.GetPrinterJobAvailableAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}