using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("number_pool")]
public class NumberPool : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("number")]
    public string Number { get; set; }
    
    [Column("is_used", TypeName = "tinyint(1)")]
    public bool IsUsed { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}