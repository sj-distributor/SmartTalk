using PostBoy.Messages.Attributes;

namespace SmartTalk.Messages.Attributes;

public class SmartTalkAuthorizeAttribute : Attribute
{
    public SmartTalkAuthorizeAttribute()
    {
    }

    public SmartTalkAuthorizeAttribute(params string[] roles)
    {
        Roles = roles;
    }

    public SmartTalkAuthorizeAttribute(string[] roles, string[] permissions)
    {
        Roles = roles;
        Permissions = permissions;
    }

    public string[] Roles { get; set; }

    public string[] Permissions { get; set; }
}