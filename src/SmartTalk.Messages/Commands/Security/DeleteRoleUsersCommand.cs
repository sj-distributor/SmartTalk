using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.MessageLogging;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkLogging(LoggingSystemType.Security)]
public class DeleteRoleUsersCommand : ICommand
{
    public List<int> RoleUserIds { get; set; }
}

public class DeleteRoleUsersResponse : SmartTalkResponse<List<RoleUserDto>>
{
}