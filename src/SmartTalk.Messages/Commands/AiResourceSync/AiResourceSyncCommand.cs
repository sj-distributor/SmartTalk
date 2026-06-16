using Mediator.Net.Contracts;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiResourceSync;

public class AiResourceSyncCommand : HasServiceProviderId, ICommand
{
    public bool IsManual { get; set; }

    public int? InitiatedByUserId { get; set; }
}

public class AiResourceSyncResponse : SmartTalkResponse<AiResourceSyncResponseData>
{
}

public class AiResourceSyncResponseData
{
    public int TotalCount { get; set; }
}
