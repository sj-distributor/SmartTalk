using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Domain.VoiceAi.PosManagement;

[Table("pos_product")]
public class PosProduct : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("product_id"), StringLength(36)]
    public string ProductId { get; set; }

    [Column("names")]
    public string NamesJson { get; set; }
    
    public Dictionary<string, string> Names
    {
        get => string.IsNullOrWhiteSpace(NamesJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(NamesJson);
        set => NamesJson = JsonSerializer.Serialize(value);
    }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("tax")]
    public string TaxJson { get; set; }
    
    public List<PosTaxDto> Tax
    {
        get => string.IsNullOrWhiteSpace(TaxJson)
            ? new List<PosTaxDto>()
            : JsonSerializer.Deserialize<List<PosTaxDto>>(TaxJson);
        set => TaxJson = JsonSerializer.Serialize(value);
    }

    [Column("category_ids"), StringLength(512)]
    public string CategoryIds { get; set; }

    [Column("modifiers")]
    public string ModifiersJson { get; set; }
    
    public List<PosProductModifierDto> Modifiers
    {
        get => string.IsNullOrWhiteSpace(ModifiersJson)
            ? new List<PosProductModifierDto>()
            : JsonSerializer.Deserialize<List<PosProductModifierDto>>(ModifiersJson);
        set => ModifiersJson = JsonSerializer.Serialize(value);
    }

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
