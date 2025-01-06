using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Dto.Security.Data;
using SmartTalk.Messages.Enums.MessageLogging;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkLogging(LoggingSystemType.Security)]
public class CreateRolesCommand : ICommand
{
    public List<CreateOrUpdateRoleDto> Roles { get; set; }
}

public class CreateRolesResponse : SmartTalkResponse<List<RoleDto>>
{
}