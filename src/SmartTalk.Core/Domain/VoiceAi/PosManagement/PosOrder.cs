using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Domain.VoiceAi.PosManagement;

[Table("pos_order")]
public class PosOrder : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

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

    [Column("order_num"), StringLength(16)]
    public string OrderNum { get; set; }

    [Column("status")]
    public int Status { get; set; } = 10;

    [Column("count")]
    public int Count { get; set; }

    [Column("tax")]
    public decimal Tax { get; set; }

    [Column("sub_total")]
    public decimal SubTotal { get; set; }

    [Column("total")]
    public decimal Total { get; set; }

    [Column("type")]
    public int Type { get; set; }

    [Column("items")]
    public string ItemsJson { get; set; }
    
    public List<PosOrderDto> Items
    {
        get => string.IsNullOrWhiteSpace(ItemsJson)
            ? new List<PosOrderDto>()
            : JsonSerializer.Deserialize<List<PosOrderDto>>(ItemsJson);
        set => ItemsJson = JsonSerializer.Serialize(value);
    }

    [Column("note"), StringLength(128)]
    public string Note { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}