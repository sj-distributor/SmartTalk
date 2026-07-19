using Mediator.Net.Contracts;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiResourceSync;

public class AiResourceSyncCommand : HasServiceProviderId, ICommand
{
    public bool IsManual { get; set; }

    public bool IsFullSync { get; set; } = false;

    public int? InitiatedByUserId { get; set; }

    public DateTimeOffset? SyncStartTime { get; set; }

    public DateTimeOffset? SyncEndTime { get; set; }
}

public class AiResourceSyncResponse : SmartTalkResponse
{
}
