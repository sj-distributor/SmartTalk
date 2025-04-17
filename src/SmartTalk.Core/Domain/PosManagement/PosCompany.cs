using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PosManagement;

[Table("pos_company")]
public class PosCompany : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("name"), StringLength(64)]
    public string Name { get; set; }
    
    [Column("description"), StringLength(512)]
    public string Description { get; set; }
    
    [Column("address"), StringLength(512)]
    public string Address { get; set; }
    
    [Column("status")]
    public bool Status { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}