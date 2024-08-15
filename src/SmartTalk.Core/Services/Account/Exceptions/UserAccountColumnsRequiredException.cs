namespace SmartTalk.Core.Services.Account.Exceptions;

public class UserAccountColumnsRequiredException : Exception
{
    public UserAccountColumnsRequiredException(params string[] columns) : base("User account columns required: " + string.Join(", ", columns))
    {
    }
}