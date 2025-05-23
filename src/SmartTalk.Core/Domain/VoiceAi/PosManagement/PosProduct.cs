using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.VoiceAi.PosManagement;

[Table("pos_product")]
public class PosProduct : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("category_id")]
    public string CategoryId { get; set; }

    [Column("product_id"), StringLength(36)]
    public string ProductId { get; set; }

    [Column("names")]
    public string Names { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("tax")]
    public string Tax { get; set; }
    
    [Column("category_ids"), StringLength(512)]
    public string CategoryIds { get; set; }

    [Column("modifiers")]
    public string Modifiers { get; set; }

    [Column("status")]
    public bool Status { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}
