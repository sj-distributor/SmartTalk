using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_conversation_detail")]
public class PhoneOrderConversationDetail : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("conversation_id")]
    public int ConversationId { get; set; }
    
    [Column("question")]
    public string Question { get; set; }
    
    [Column("answer")]
    public string Answer { get; set; }
    
    [Column("order")]
    public int Order { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}