using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Events.AiResourceSync;

public class AiResourceSyncEvent : IEvent
{
   public int ServiceProviderId { get; set; }

   public bool IsManual { get; set; }

   public int? InitiatedByUserId { get; set; }

   public int TotalCount { get; set; }
}
