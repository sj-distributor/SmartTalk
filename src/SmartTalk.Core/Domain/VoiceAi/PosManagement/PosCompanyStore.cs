using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.VoiceAi.PosManagement;

[Table("pos_company_store")]
public class PosCompanyStore : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("company_id")]
    public int CompanyId { get; set; }
    
    [Column("en_name"), StringLength(64)]
    public string EnName { get; set; }
    
    [Column("zh_name"), StringLength(64)]
    public string ZhName { get; set; }
    
    [Column("description"), StringLength(512)]
    public string Description { get; set; }
    
    [Column("status")]
    public bool Status { get; set; }
    
    [Column("phone_nums"), StringLength(64)]
    public string PhoneNums { get; set; }
    
    [Column("logo"), StringLength(1024)]
    public string Logo { get; set; }
    
    [Column("address"), StringLength(512)]
    public string Address { get; set; }
    
    [Column("latitude"), StringLength(16)]
    public string Latitude { get; set; }
    
    [Column("longitude"), StringLength(16)]
    public string Longitude { get; set; }
    
    [Column("link"), StringLength(512)]
    public string Link { get; set; }
    
    [Column("apple_id"), StringLength(128)]
    public string AppleId { get; set; }
    
    [Column("app_secret"), StringLength(512)]
    public string AppSecret { get; set; }
    
    [Column("pos_display"), StringLength(128)]
    public string PosDisPlay { get; set; }
    
    [Column("pos_id"), StringLength(128)]
    public string PosId { get; set; }
    
    [Column("is_link"), StringLength(16)]
    public bool IsLink { get; set; }
    
    [Column("created_by")]
    public int CreatedBy { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }
    
    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}