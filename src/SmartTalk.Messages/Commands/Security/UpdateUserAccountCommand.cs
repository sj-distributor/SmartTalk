using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkAuthorize("CanUpdateAccount")]
public class UpdateUserAccountCommand : ICommand
{
    public int UserId { get; set; }

    public int RoleId { get; set; } 

    public string RoleName { get; set; }
}

public class UpdateUserAccountResponse : SmartTalkResponse
{
    public int Id { get; set; }
    
    public int RoleId { get; set; }
    
    public int UserId { get; set; }
}