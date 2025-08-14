using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Core.Handlers.RequestHandlers.Printer;

public class UploadOrderPrintImageAndUpdatePrintUrlRequestHandler : IRequestHandler<UploadOrderPrintImageAndUpdatePrintUrlRequest, UploadOrderPrintImageAndUpdatePrintUrlResponse>
{
    private readonly IPrinterService _printerService;

    public UploadOrderPrintImageAndUpdatePrintUrlRequestHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    public async Task<UploadOrderPrintImageAndUpdatePrintUrlResponse> Handle(IReceiveContext<UploadOrderPrintImageAndUpdatePrintUrlRequest> context, CancellationToken cancellationToken)
    {
        var imageUrl = await _printerService.UploadOrderPrintImageToQiNiuAndUpdatePrintUrlAsync(context.Message.JobToken,
            context.Message.PrintDate,
            cancellationToken);

        return new UploadOrderPrintImageAndUpdatePrintUrlResponse
        {
            ImageUrl = imageUrl
        };
    }
}