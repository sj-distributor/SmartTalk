namespace SmartTalk.Messages.Dto.Pos;

public class PosMenuDto
{
    public int Id { get; set; }

    public int StoreId { get; set; }

    public long MenuId { get; set; }

    public string Names { get; set; }

    public string TimePeriod { get; set; }

    public string CategoryIds { get; set; }

    public bool Status { get; set; }

    public int? CreatedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public int? LastModifiedBy { get; set; }

    public DateTimeOffset? LastModifiedDate { get; set; }
}