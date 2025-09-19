using System;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Message.Events.Printer
{
    public class PrinterStatusChangedEvent : IEvent
    {
        public string PrinterMac { get; set; }

        public Guid Token { get; set; }
        
        public PrinterStatusInfo OldPrinterStatusInfo { get; set; }
        
        public PrinterStatusInfo NewPrinterStatusInfo { get; set; }

        public bool Skip()
        {
            return OldPrinterStatusInfo == null || NewPrinterStatusInfo == null;
        }
    }
}