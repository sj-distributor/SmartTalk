using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SmartTalk.Core.Domain.VoiceAi.PosManagement;

[Table("pos_category")]
public class PosCategory : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("menu_id")]
    public int MenuId { get; set; }

    [Column("category_id"), StringLength(36)]
    public string CategoryId { get; set; }
    
    [Column("names"), Required]
    public string NamesJson { get; set; }
    
    public Dictionary<string, string> Names
    { 
        get => string.IsNullOrWhiteSpace(NamesJson) 
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(NamesJson);
        set => NamesJson = JsonSerializer.Serialize(value);
    }

    [Column("menu_ids"), Required]
    public string MenuIds { get; set; }

    [Column("menu_names"), Required]
    public string MenuNames { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }

    [Column("created_by")] 
    public int CreatedBy { get; set; }

    [Column("created_date")] 
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}