using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Messages.Dto.Pos;

public class PosOrderDto
{
    public int Id { get; set; }

    public int StoreId { get; set; }
    
    public int? RecordId { get; set; }

    public string Name { get; set; }

    public string Phone { get; set; }

    public string Address { get; set; }

    public string Latitude { get; set; }

    public string Longitude { get; set; }

    public string Room { get; set; }
    
    public string Remarks { get; set; }

    public string OrderNo { get; set; }
    
    public string OrderId { get; set; }

    public PosOrderStatus Status { get; set; }

    public int Count { get; set; }

    public decimal Tax { get; set; }

    public decimal SubTotal { get; set; }

    public decimal Total { get; set; }

    public PosOrderReceiveType Type { get; set; }

    public string Items { get; set; }
    
    public string ModifiedItems { get; set; }
    
    public bool IsPush { get; set; }

    public string Notes { get; set; }
    
    public int RetryCount { get; set; }
    
    public PosOrderModifiedStatus ModifiedStatus { get; set; }

    public int? CreatedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public int? LastModifiedBy { get; set; }

    public DateTimeOffset? LastModifiedDate { get; set; }
    
    public int? SentBy { get; set; }
    
    public DateTimeOffset? SentTime { get; set; }
    
    public string SentByUsername { get; set; }
    
    public List<PosProductSimpleModifiersDto> SimpleModifiers { get; set; } = [];
}