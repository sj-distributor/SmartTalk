using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

public class GetPermissionByIdRequest : IRequest
{
    public int Id { get; set; }
}
public class GetPermissionByIdResponse : SmartTalkResponse<PermissionDto>
{
    
}