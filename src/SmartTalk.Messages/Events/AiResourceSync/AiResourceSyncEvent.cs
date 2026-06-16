using Mediator.Net.Contracts;
using SmartTalk.Messages.Commands.AiResourceSync;

namespace SmartTalk.Messages.Events.AiResourceSync;

public class AiResourceSyncEvent : IEvent
{
   public AiResourceSyncCommand Command { get; set; }

   public int TotalCount { get; set; }
}
