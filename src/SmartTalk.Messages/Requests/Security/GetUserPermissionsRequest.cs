using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Security;

namespace SmartTalk.Messages.Requests.Security;

public class GetUserPermissionsRequest : IRequest
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 15;

    public string Keyword { get; set; }
}

public class GetUserPermissionsResponse : SmartTalkResponse<GetUserPermissionsResponseData>
{
}

public class GetUserPermissionsResponseData
{
    public int Count { get; set; }
    
    public List<UserPermissionDto> UserPermissions { get; set; }
}