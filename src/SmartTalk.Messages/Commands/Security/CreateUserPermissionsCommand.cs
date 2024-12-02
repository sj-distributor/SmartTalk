using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.DTO.Security.Data;

namespace SmartTalk.Messages.Commands.Security;

public class CreateUserPermissionsCommand : ICommand
{
    public List<CreateOrUpdateUserPermissionDto> UserPermissions { get; set; }
}

public class CreateUserPermissionsResponse : SmartTalkResponse<List<UserPermissionDto>>
{
}