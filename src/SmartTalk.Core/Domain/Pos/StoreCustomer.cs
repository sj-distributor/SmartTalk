using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Pos;

[Table("store_customer")]
public class StoreCustomer : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("name"), StringLength(255)]
    public string Name { get; set; }

    [Column("phone"), StringLength(64)]
    public string Phone { get; set; }

    [Column("address"), StringLength(512)]
    public string Address { get; set; }

    [Column("latitude"), StringLength(16)]
    public string Latitude { get; set; }

    [Column("longitude"), StringLength(16)]
    public string Longitude { get; set; }
    
    [Column("room")]
    public string Room { get; set; }

    [Column("notes"), StringLength(512)]
    public string Notes { get; set; }
    
    [Column("timezone"), StringLength(64)]
    public string Timezone { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}