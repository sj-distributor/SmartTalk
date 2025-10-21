using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkAuthorize("CanDeleteAccount")]
public class DeleteUserAccountsCommand : ICommand
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public string UserName { get; set; }
}

public class DeleteUserAccountsResponse : SmartTalkResponse
{
}