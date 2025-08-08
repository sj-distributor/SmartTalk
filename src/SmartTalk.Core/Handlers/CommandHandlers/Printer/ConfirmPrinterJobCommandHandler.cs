using AutoMapper;
using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Events.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer;

public class ConfirmPrinterJobCommandHandler : ICommandHandler<ConfirmPrinterJobCommand>
{
    private readonly IPrinterService _printerService;

    public ConfirmPrinterJobCommandHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    public async Task Handle(IReceiveContext<ConfirmPrinterJobCommand> context, CancellationToken cancellationToken)
    {
        PrinterJobConfirmedEvent @event;
        if (context.Message.PrintSuccessfully)
        {
            @event = await _printerService.ConfirmPrinterJob(context.Message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            @event = await _printerService.RecordPrintErrorAfterConfirmPrinterJob(context.Message, cancellationToken).ConfigureAwait(false);
        }

        if (@event != null)
        {
            await context.PublishAsync(@event, cancellationToken);
        }
    }
}