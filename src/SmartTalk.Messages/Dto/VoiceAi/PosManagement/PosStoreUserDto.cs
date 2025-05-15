using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Messages.Dto.VoiceAi.PosManagement;

public class PosStoreUserDto
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    
    public string UserName { get; set; }
    
    public int StoreId { get; set; }
    
    public int? CreatedBy { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int? LastModifiedBy { get; set; }
    
    public DateTimeOffset? LastModifiedDate { get; set; }
}