using Mediator.Net.Contracts;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.Security;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

public class GetRolePermissionsRequest : IRequest
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 15;
    
    public string Keyword { get; set; }
    
    public RoleSystemSource SystemSource { get; set; }
}

public class GetRolePermissionsResponse : SmartTalkResponse<GetRolePermissionsResponseData>
{
}

public class GetRolePermissionsResponseData
{
    public int Count { get; set; }
    
    public List<RolePermissionDto> RolePermissions { get; set; }
}
