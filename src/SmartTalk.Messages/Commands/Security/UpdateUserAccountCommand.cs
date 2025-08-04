using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanUpdateAccount })]
public class UpdateUserAccountCommand : ICommand
{
    public int UserId { get; set; }

    public int OldRoleId { get; set; } 

    public int NewRoleId { get; set; }
    
    public List<int> CompanyIds { get; set; }
    
    public List<int> StoreIds { get; set; }
}

public class UpdateUserAccountResponse : SmartTalkResponse<UpdateUserAccountDto>
{
}

public class UpdateUserAccountDto
{
    public int Id { get; set; }
    
    public int RoleId { get; set; }
    
    public int UserId { get; set; }
}