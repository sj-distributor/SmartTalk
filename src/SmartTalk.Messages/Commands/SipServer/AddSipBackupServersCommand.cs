using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.SipServer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.SipServer;

public class AddSipBackupServersCommand : ICommand
{
    public List<SipBackupServerDto> BackupServers { get; set; }
}

public class AddSipBackupServersResponse : SmartTalkResponse<List<SipBackupServerDto>>
{
}