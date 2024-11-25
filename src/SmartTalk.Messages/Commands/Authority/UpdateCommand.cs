using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;

namespace SmartTalk.Messages.Commands.Authority;

[SmartTalkAuthorize("CanUpdateAccount")]
public class UpdateCommand : ICommand
{
    public int UserId { get; set; }

    public int RoleId { get; set; } 

    public string RoleName { get; set; }
}

public class UpdateResponse : SmartTalkResponse
{
    public int Id { get; set; }
    
    public int RoleId { get; set; }
    
    public int UserId { get; set; }
}