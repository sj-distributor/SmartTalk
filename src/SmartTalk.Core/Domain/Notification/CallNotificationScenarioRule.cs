using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Notification;

[Table("call_notification_scenario_rule")]
public class CallNotificationScenarioRule : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("scenario_key"), StringLength(64)]
    public string ScenarioKey { get; set; }

    [Column("description"), StringLength(1024)]
    public string Description { get; set; }

    [Column("prompt_template")]
    public string PromptTemplate { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}
