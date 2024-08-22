using SmartTalk.Core.Services.Identity;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.IntegrationTests;

public class TestCurrentUser : ICurrentUser
{
    public int? Id { get; set; } = 1;

    public string Name { get; } = "TEST_USER";

    public UserAccountIssuer? AuthType { get; set; }

    public string UserName { get; set; } = "TEST_USER";
}