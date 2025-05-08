using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Domain.VoiceAi.PosManagement;

[Table("pos_menu")]
public class PosMenu : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("menu_id"), StringLength(36)]
    public string MenuId { get; set; }

    [Column("names")]
    public string NamesJson { get; set; }
    
    public Dictionary<string, string> Names
    {
        get => string.IsNullOrWhiteSpace(NamesJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(NamesJson);
        set => NamesJson = JsonSerializer.Serialize(value);
    }

    [Column("time_period")]
    public string TimePeriodJson { get; set; }
    
    public List<PosTimePeriodDto> TimePeriods
    {
        get => string.IsNullOrWhiteSpace(TimePeriodJson)
            ? new List<PosTimePeriodDto>()
            : JsonSerializer.Deserialize<List<PosTimePeriodDto>>(TimePeriodJson);
        set => TimePeriodJson = JsonSerializer.Serialize(value);
    }

    [Column("category_ids"), StringLength(512)]
    public string CategoryIds { get; set; }

    [Column("status")]
    public bool Status { get; set; }

    [Column("created_by")]
    public int CreatedBy { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}