using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PosManagement;

[Table("pos_company_store_user")]
public class PosCompanyStoreUser : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("user_id")]
    public int UserId { get; set; }
    
    [Column("store_id")]
    public int StoreId { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}