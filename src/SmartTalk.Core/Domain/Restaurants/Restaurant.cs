using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Restaurants;

[Table("restaurant")]
public class Restaurant : IEntity<int>, IAgent
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("name"), StringLength(128)]
    public string Name { get; set; }

    [Column("another_name"), StringLength(128)]
    public string AnotherName { get; set; }
    
    [Column("message"), StringLength(1024)]
    public string Message { get; set; }

    [Column("phone_number"), StringLength(125)]
    public string PhoneNumber { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}