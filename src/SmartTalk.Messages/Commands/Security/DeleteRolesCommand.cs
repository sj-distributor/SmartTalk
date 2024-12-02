using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.MessageLogging;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkLogging(LoggingSystemType.Security)]
public class DeleteRolesCommand : ICommand
{
    public List<int> RoleIds { get; set; }
}

public class DeleteRolesResponse : SmartTalkResponse<List<RoleDto>>
{
}