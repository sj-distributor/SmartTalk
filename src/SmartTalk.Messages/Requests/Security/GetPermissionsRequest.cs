using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Security;

namespace SmartTalk.Messages.Requests.Security;

public class GetPermissionsRequest : IRequest
{
    public int? PageIndex { get; set; }

    public int? PageSize { get; set; }
}

public class GetPermissionsResponse : SmartTalkResponse<GetPermissionsResponseData>
{
}

public class GetPermissionsResponseData
{
    public int Count { get; set; }
    
    public List<PermissionDto> Permissions { get; set; }
}