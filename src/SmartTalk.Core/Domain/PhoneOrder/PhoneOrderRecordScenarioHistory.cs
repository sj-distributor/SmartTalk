using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_record_scenario_history")]
public class PhoneOrderRecordScenarioHistory : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("record_id")]
    public int RecordId { get; set; }
    
    [Column("scenario")]
    public DialogueScenarios Scenario { get; set; }

    [Column("modify_type")]
    public ModifyType ModifyType { get; set; }
    
    [Column("updated_by")]
    public int UpdatedBy { get; set; } 
    
    [Column("username")]
    public string UserName { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}