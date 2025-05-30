namespace SmartTalk.Messages.Dto.VoiceAi.PosManagement;

public class PosCategoryDto
{
    public int Id { get; set; }
    
    public int StoreId { get; set; }

    public int MenuId { get; set; }

    public string CategoryId { get; set; }

    public string Names { get; set; }

    public string MenuIds { get; set; }

    public string MenuNames { get; set; }

    public int? SortOrder { get; set; }

    public int? CreatedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public int? LastModifiedBy { get; set; }

    public DateTimeOffset? LastModifiedDate { get; set; }
}