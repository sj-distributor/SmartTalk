using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.Security;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

public class GetPermissionsByRolesRequest : IRequest
{
    public int? PageIndex { get; set; }

    public int? PageSize { get; set; }
    
    public string Keyword { get; set; }
    
    public RoleSystemSource? SystemSource { get; set; }
}

public class GetPermissionsByRolesResponse : SmartTalkResponse<GetPermissionsByRolesResponseData>
{
}

public class GetPermissionsByRolesResponseData
{
    public int Count { get; set; }
    
    public List<RolePermissionDataDto> RolePermissionData { get; set; }
}