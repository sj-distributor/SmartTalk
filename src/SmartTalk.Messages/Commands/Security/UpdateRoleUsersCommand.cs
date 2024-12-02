using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.DTO.Security.Data;
using SmartTalk.Messages.Enums.MessageLogging;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkLogging(LoggingSystemType.Security)]
public class UpdateRoleUsersCommand : ICommand
{
    public List<CreateOrUpdateRoleUserDto> RoleUsers { get; set; }
}

public class UpdateRoleUsersResponse : SmartTalkResponse<List<RoleUserDto>>
{
}