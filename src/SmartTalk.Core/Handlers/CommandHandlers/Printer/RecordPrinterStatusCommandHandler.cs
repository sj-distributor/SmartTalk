using System;
using System.Threading;
using System.Threading.Tasks;
using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Microsoft.Extensions.Caching.Memory;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer
{
    public class RecordPrinterStatusCommandHandler : ICommandHandler<RecordPrinterStatusCommand>
    {
        private readonly IPrinterService _printerService;
        private readonly IMemoryCache _memoryCache;

        public RecordPrinterStatusCommandHandler(IPrinterService printerService,IMemoryCache memoryCache)
        {
            _printerService = printerService;
            _memoryCache = memoryCache;
        }
        
        public async Task Handle(IReceiveContext<RecordPrinterStatusCommand> context, CancellationToken cancellationToken)
        {
            var cacheKey = $"RecordPrinterStatusKey_{context.Message.PrinterMac}";
            if (!_memoryCache.TryGetValue(cacheKey, out object _))
            {
                var printerStatusChangedEvent = await _printerService.RecordPrinterStatus(context.Message, cancellationToken);
                _memoryCache.Set(cacheKey, 1, TimeSpan.FromMinutes(1));

                await context.PublishAsync(printerStatusChangedEvent,cancellationToken);
            }
        }
    }
}