using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartTalk.Core.Constants;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Core.Services.Identity;

public interface ICurrentUser
{
    int? Id { get; }
    
    string Name { get; }
    
    UserAccountIssuer? AuthType { get; }
}

public class ApiUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? Id
    {
        get
        {
            if (_httpContextAccessor?.HttpContext == null) return null;

            var idClaim = _httpContextAccessor.HttpContext.User.Claims
                .SingleOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            
            return int.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string Name
    {
        get
        {
            return _httpContextAccessor?.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
        }
    }
    
    public UserAccountIssuer? AuthType
    {
        get
        {
            if (_httpContextAccessor?.HttpContext == null) return null;

            return _httpContextAccessor.HttpContext.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Authentication)?.Value switch
            {
                AuthenticationSchemeConstants.SelfAuthenticationScheme => UserAccountIssuer.Self,
                AuthenticationSchemeConstants.WiltechsAuthenticationScheme => UserAccountIssuer.Wiltechs,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}

public class InternalUser : ICurrentUser
{
    public int? Id => CurrentUsers.InternalUser.Id;

    public string Name => "internal_user";

    public UserAccountIssuer? AuthType => UserAccountIssuer.Self;
}