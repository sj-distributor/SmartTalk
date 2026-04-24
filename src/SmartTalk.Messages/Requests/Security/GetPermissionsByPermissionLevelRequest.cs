using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.Enums.Security;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

public class GetPermissionsByPermissionLevelRequest : IRequest
{
    public PermissionLevel PermissionLevel { get; set; }
}

public class GetPermissionsByPermissionLevelResponse : SmartTalkResponse<List<PermissionDto>>
{
}