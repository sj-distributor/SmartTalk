using System.Net;

namespace SmartTalk.Core.Middlewares.Security;

public class AccountExpiredException : Exception
{
    public AccountExpiredException( string message) : base(message)
    {
    }
}