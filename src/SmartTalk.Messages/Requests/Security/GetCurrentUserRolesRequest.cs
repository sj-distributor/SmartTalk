using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.Security;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

public class GetCurrentUserRolesRequest : IRequest
{
    public RoleSystemSource? SystemSource { get; set; }
}

public class GetCurrentUserRolesResponse : SmartTalkResponse<GetCurrentUserRolesResponseData>
{
}

public class GetCurrentUserRolesResponseData
{
    public int Count { get; set; }

    public UserAccountDto UserAccount { get; set; }
    
    public List<RolePermissionDataDto> RolePermissionData { get; set; }
}