using Mediator.Net.Contracts;
using Smarties.Messages.Attributes;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Authority;

[SmartiesAuthorize("CanDeleteAccount")]
public class DeleteAccountsCommand : ICommand
{
    public string UserName { get; set; }

    public int roleId { get; set; }
}

public class DeleteAccountsResponse : SmartTalkResponse
{
}