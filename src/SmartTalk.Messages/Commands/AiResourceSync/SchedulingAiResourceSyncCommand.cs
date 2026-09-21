using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.AiResourceSync;

public class SchedulingAiResourceSyncCommand : ICommand
{
    public int? ServiceProviderId { get; set; }
}
