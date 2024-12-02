using Mediator.Net.Contracts;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.DTO.Security.Data;
using SmartTalk.Messages.Enums.MessageLogging;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkLogging(LoggingSystemType.Security)]
public class CreateRoleUsersCommand : ICommand
{
    public List<CreateOrUpdateRoleUserDto> RoleUsers { get; set; }
}

public class CreateRoleUsersResponse : SmartTalkResponse<List<RoleUserDto>>
{
}