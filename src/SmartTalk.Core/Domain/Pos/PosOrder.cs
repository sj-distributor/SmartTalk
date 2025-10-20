using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Core.Domain.Pos;

[Table("pos_order")]
public class PosOrder : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }
    
    [Column("record_id")]
    public int? RecordId { get; set; }

    [Column("name"), StringLength(64)]
    public string Name { get; set; }

    [Column("phone"), StringLength(16)]
    public string Phone { get; set; }

    [Column("address"), StringLength(512)]
    public string Address { get; set; }

    [Column("latitude"), StringLength(16)]
    public string Latitude { get; set; }

    [Column("longitude"), StringLength(16)]
    public string Longitude { get; set; }

    [Column("room"), StringLength(64)]
    public string Room { get; set; }
    
    [Column("remarks"), StringLength(512)]
    public string Remarks { get; set; }

    [Column("order_no"), StringLength(16)]
    public string OrderNo { get; set; }
    
    [Column("order_id"), StringLength(32)]
    public string OrderId { get; set; }

    [Column("status")]
    public PosOrderStatus Status { get; set; } = PosOrderStatus.Pending;

    [Column("count")]
    public int Count { get; set; }

    [Column("tax")]
    public decimal Tax { get; set; }

    [Column("sub_total")]
    public decimal SubTotal { get; set; }

    [Column("total")]
    public decimal Total { get; set; }

    [Column("type")]
    public PosOrderReceiveType Type { get; set; }

    [Column("items")]
    public string Items { get; set; }
    
    [Column("modified_items")]
    public string ModifiedItems { get; set; }
    
    [Column("is_push")]
    public bool IsPush { get; set; }

    [Column("notes"), StringLength(128)]
    public string Notes { get; set; }
    
    [Column("retry_count")]
    public int RetryCount { get; set; }
    
    [Column("modified_status")]
    public PosOrderModifiedStatus ModifiedStatus { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}