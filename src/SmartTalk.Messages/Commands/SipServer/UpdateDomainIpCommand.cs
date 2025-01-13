using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using AlibabaCloud.SDK.Alidns20150109.Models;

namespace SmartTalk.Messages.Commands.SipServer;

public class UpdateDomainIpCommand : ICommand
{
    public string DescribeDomain { get; set; }

    public string Endpoint { get; set; }

    public string HostRecords { get; set; }

    public string Value { get; set; }
}

public class UpdateDomainIpResponse : SmartTalkResponse<UpdateDomainRecordResponseBody>
{
}