using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Messages.Requests.Security;

public class GetRolesRequest : IRequest
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 15;
    
    public string Keyword { get; set; }
    
    public RoleSystemSource? SystemSource { get; set; }
}

public class GetRolesResponse : SmartTalkResponse<GetRolesResponseData>
{
}

public class GetRolesResponseData
{
    public int Count { get; set; }
    
    public List<RoleDto> Roles { get; set; }
}