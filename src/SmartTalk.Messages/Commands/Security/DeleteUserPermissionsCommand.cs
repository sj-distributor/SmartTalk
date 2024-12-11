using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Security;

namespace SmartTalk.Messages.Commands.Security;

public class DeleteUserPermissionsCommand : ICommand
{
    public List<int> UserPermissionIds { get; set; }
}

public class DeleteUserPermissionsResponse : SmartTalkResponse<List<UserPermissionDto>>
{
}