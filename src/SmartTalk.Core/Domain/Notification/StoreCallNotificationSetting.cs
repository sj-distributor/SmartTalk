using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Notification;

[Table("store_call_notification_setting")]
public class StoreCallNotificationSetting : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("scenario_key"), StringLength(64)]
    public string ScenarioKey { get; set; }

    [Column("is_enabled")]
    public bool IsEnabled { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}
