using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Security;

namespace SmartTalk.Messages.Events.Security;

public class UserPermissionsCreatedEvent : IEvent
{
    public List<UserPermissionDto> UserPermissions { get; set; }
}