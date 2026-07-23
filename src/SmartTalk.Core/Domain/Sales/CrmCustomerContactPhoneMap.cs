using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Sales;

[Table("crm_customer_contact_phone_map")]
public class CrmCustomerContactPhoneMap : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("assistant_id")]
    public int AssistantId { get; set; }

    [Column("customer_id"), StringLength(64)]
    public string CustomerId { get; set; }

    [Column("customer_name"), StringLength(255)]
    public string CustomerName { get; set; }

    [Column("contact_name"), StringLength(255)]
    public string ContactName { get; set; }

    [Column("contact_identity"), StringLength(255)]
    public string ContactIdentity { get; set; }

    [Column("contact_language"), StringLength(64)]
    public string ContactLanguage { get; set; }

    [Column("contact_phone_raw"), StringLength(64)]
    public string ContactPhoneRaw { get; set; }

    [Column("contact_phone_normalized"), StringLength(32)]
    public string ContactPhoneNormalized { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}
