using Mediator.Net.Contracts;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

public class GetRoleByIdRequest : IRequest
{
    public int Id { get; set; }
}

public class GetRoleByIdResponse : SmartTalkResponse<RoleDto>
{
}