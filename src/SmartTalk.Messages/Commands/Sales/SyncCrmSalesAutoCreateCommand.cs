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
}