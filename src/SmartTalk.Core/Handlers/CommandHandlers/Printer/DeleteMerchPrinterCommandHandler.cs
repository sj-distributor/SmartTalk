using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer;

public class DeleteMerchPrinterCommandHandler : ICommandHandler<DeleteMerchPrinterCommand, DeleteMerchPrinterResponse>
{
    private readonly IPrinterService _printerService;

    public DeleteMerchPrinterCommandHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    public async Task<DeleteMerchPrinterResponse> Handle(IReceiveContext<DeleteMerchPrinterCommand> context, CancellationToken cancellationToken)
    {
        return await _printerService.DeleteMerchPrinterAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}