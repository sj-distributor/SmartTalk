using System.Text;
using Mediator.Net.Contracts;
using System.Security.Cryptography;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkAuthorize(Permissions = [SecurityStore.Permissions.CanCreateAccount])]
public class CreateUserAccountCommand : ICommand
{
    public string UserName { get; set; }
    
    public int RoleId { get; set; }
    
    public string OriginalPassword => _originalPassword ?? GenerateRandomPassword(6);
    
    private string _originalPassword;
    
    private static string GenerateRandomPassword(int length)
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*()_-+=<>?";
        const string allCharacters = uppercase + lowercase + digits + specialChars;

        var password = new StringBuilder();
        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[4];

        for (var i = 0; i < length; i++)
        {
            rng.GetBytes(buffer);
            var randomIndex = BitConverter.ToUInt32(buffer, 0) % allCharacters.Length;
            password.Append(allCharacters[(int)randomIndex]);
        }

        return password.ToString();
    }
}

public class CreateUserAccountResponse : SmartTalkResponse<UserAccountDto>
{
}