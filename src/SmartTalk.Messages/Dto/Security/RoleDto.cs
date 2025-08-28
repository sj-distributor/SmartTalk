using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Messages.DTO.Security;

public class RoleDto
{
    public RoleDto()
    {
        CreatedDate = DateTimeOffset.Now;
        ModifiedDate = DateTimeOffset.Now;
    }
    
    public int Id { get; set; }
    
    public int? PosServiceId { get; set; }
    
    public UserAccountLevel UserAccountLevel { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public DateTimeOffset ModifiedDate { get; set; }
    
    public string Name { get; set; }
    
    public string DisplayName => GetDisplayName(Name);
    
    public RoleSystemSource SystemSource { get; set; }
    
    public string Description { get; set; }
    
    public bool IsSystem { get; set; }

    private string GetDisplayName(string name)
    {
        return name switch
        {
            "SuperAdministrator" => "超级管理员",
            "Administrator" => "管理员",
            "User" => "操作员",
            "Operator" => "操作员",
            "ServiceProviderOperator" => "操作员",
            _ => null
        };
    }
}