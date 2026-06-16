using Mediator.Net.Contracts;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Messages.Events.AiResourceSync;

public class AiResourceSyncEvent : IEvent
{
   public AiResourceSyncCommand Command { get; set; }

   public List<CrmSalesAutoSyncCustomerDto> CrmSalesAuto { get; set; } = new();
}
