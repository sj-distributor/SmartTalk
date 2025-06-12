namespace SmartTalk.Messages.Dto.Pos;

public class PosProductDto
{
    public int Id { get; set; }
    
    public int StoreId { get; set; }

    public int CategoryId { get; set; }

    public long ProductId { get; set; }

    public string Names { get; set; }

    public decimal Price { get; set; }

    public string Tax { get; set; }

    public string CategoryIds { get; set; }

    public string Modifiers { get; set; }

    public bool Status { get; set; }

    public int? SortOrder { get; set; }

    public int? CreatedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public int? LastModifiedBy { get; set; }

    public DateTimeOffset? LastModifiedDate { get; set; }
}