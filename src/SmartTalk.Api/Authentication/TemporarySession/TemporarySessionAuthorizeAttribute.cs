using Microsoft.AspNetCore.Authorization;

namespace SmartTalk.Api.Authentication.TemporarySession;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TemporarySessionAuthorizeAttribute : AuthorizeAttribute
{
    public TemporarySessionAuthorizeAttribute()
    {
        Policy = TemporarySessionAuthenticationDefaults.TemporarySessionPolicy;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class AccountOrTemporarySessionAuthorizeAttribute : AuthorizeAttribute
{
    public AccountOrTemporarySessionAuthorizeAttribute()
    {
        Policy = TemporarySessionAuthenticationDefaults.AccountOrTemporarySessionPolicy;
    }
}
