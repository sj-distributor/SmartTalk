using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Core.Handlers.RequestHandlers.Printer;

public class UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequestHandler : IRequestHandler<UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequest, UploadOrderPrintImageToQiNiuAndUpdatePrintUrlResponse>
{
    private readonly IPrinterService _printerService;

    public UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequestHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    public async Task<UploadOrderPrintImageToQiNiuAndUpdatePrintUrlResponse> Handle(IReceiveContext<UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequest> context, CancellationToken cancellationToken)
    {
        var imageUrl = await _printerService.UploadOrderPrintImageToQiNiuAndUpdatePrintUrlAsync(context.Message.JobToken,
            context.Message.PrintDate,
            cancellationToken);

        return new UploadOrderPrintImageToQiNiuAndUpdatePrintUrlResponse
        {
            ImageUrl = imageUrl
        };
    }
}