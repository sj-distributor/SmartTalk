using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Dto.Security.Data;
using SmartTalk.Messages.Enums.MessageLogging;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkLogging(LoggingSystemType.Security)]
public class UpdateRolesCommand : ICommand
{
    public List<CreateOrUpdateRoleDto> Roles { get; set; }
}

public class UpdateRolesResponse : SmartTalkResponse<List<RoleDto>>
{
}