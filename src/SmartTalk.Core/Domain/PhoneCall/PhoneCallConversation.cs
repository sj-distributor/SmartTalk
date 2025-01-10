using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PhoneCall;

[Table("phone_call_conversation")]
public class PhoneCallConversation : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("record_id")]
    public int RecordId { get; set; }
    
    [Column("question")]
    public string Question { get; set; }
    
    [Column("answer")]
    public string Answer { get; set; }
    
    [Column("order")]
    public int Order { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}