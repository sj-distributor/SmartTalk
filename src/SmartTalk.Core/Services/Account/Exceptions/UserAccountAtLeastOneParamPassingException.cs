namespace SmartTalk.Core.Services.Account.Exceptions;

public class UserAccountAtLeastOneParamPassingException : Exception
{
    public UserAccountAtLeastOneParamPassingException() 
        : base("At least one param must be passed")
    {
    }
}