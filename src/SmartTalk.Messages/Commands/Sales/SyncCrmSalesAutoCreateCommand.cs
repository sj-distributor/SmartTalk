using Mediator.Net.Contracts;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Sales;

public class SyncCrmSalesAutoCreateCommand : HasServiceProviderId, ICommand
{
    public bool IsManual { get; set; }
}

public class SyncCrmSalesAutoCreateResponse : SmartTalkResponse<SyncCrmSalesAutoCreateResponseData>
{
}

public class SyncCrmSalesAutoCreateResponseData
{
    public int TotalCount { get; set; }

    public int CreatedStoreCount { get; set; }

    public int CreatedAssistantCount { get; set; }

    public int CreatedKnowledgeCount { get; set; }

    public int AppliedSceneCount { get; set; }

    public int TransferredAssistantCount { get; set; }

    public int DeactivatedAssistantCount { get; set; }

    public bool IsInitialRelease { get; set; }

    public bool SkippedDueToInitialReleaseDay { get; set; }

    public List<string> Warnings { get; set; } = new();
}
