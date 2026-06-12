using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Sales;

[Table("crm_sales_auto_sync_run")]
public class CrmSalesAutoSyncRun : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("mode"), StringLength(32)]
    public string Mode { get; set; }

    [Column("is_success", TypeName = "tinyint(1)")]
    public bool IsSuccess { get; set; }

    [Column("total_count")]
    public int TotalCount { get; set; }

    [Column("created_store_count")]
    public int CreatedStoreCount { get; set; }

    [Column("created_agent_count")]
    public int CreatedAgentCount { get; set; }

    [Column("created_assistant_count")]
    public int CreatedAssistantCount { get; set; }

    [Column("created_knowledge_count")]
    public int CreatedKnowledgeCount { get; set; }

    [Column("applied_scene_count")]
    public int AppliedSceneCount { get; set; }

    [Column("transferred_assistant_count")]
    public int TransferredAssistantCount { get; set; }

    [Column("deactivated_assistant_count")]
    public int DeactivatedAssistantCount { get; set; }

    [Column("warnings_json"), StringLength(4000)]
    public string WarningsJson { get; set; }

    [Column("details_json", TypeName = "longtext")]
    public string DetailsJson { get; set; }

    [Column("error_message"), StringLength(4000)]
    public string ErrorMessage { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
}
