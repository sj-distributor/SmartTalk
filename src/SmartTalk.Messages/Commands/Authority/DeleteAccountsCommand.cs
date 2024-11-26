using Mediator.Net.Contracts;
using Smarties.Messages.Attributes;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Authority;

[SmartiesAuthorize("CanDeleteAccount")]
public class DeleteAccountsCommand : ICommand
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public string UserName { get; set; }
}

public class DeleteAccountsResponse : SmartTalkResponse
{
}