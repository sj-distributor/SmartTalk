using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.SipServer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.SipServer;

public class AddSipHostServersCommand : ICommand
{
    public List<SipHostServerDto> HostServers { get; set; }
}

public class AddSipHostServersResponse : SmartTalkResponse<List<SipHostServerDto>>
{
}