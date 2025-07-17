using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Pos;

[Table("pos_company_user")]
public class PosCompanyUser : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("user_id")]
    public int UserId { get; set; }
    
    [Column("company_id")]
    public int CompanyId { get; set; }
    
    [Column("created_by")]
    public int? CreatedBy { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }
    
    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}