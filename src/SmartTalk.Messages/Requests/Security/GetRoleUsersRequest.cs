using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.DTO.Security;

namespace SmartTalk.Messages.Requests.Security;

public class GetRoleUsersRequest : IRequest
{
    public int? PageIndex { get; set; }

    public int? PageSize { get; set; }
    
    public int? RoleId { get; set; }
    
    public string Keyword { get; set; }
}

public class GetRoleUsersResponse : SmartTalkResponse<GetRoleUsersResponseData>
{
}

public class GetRoleUsersResponseData
{
    public int Count { get; set; }
    
    public List<RoleUserDto> RoleUsers { get; set; }
}