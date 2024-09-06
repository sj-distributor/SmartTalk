using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

[Table("phone_order_conversation")]
public class PhoneOrderConversationDto
{
    public int Id { get; set; }

    public int RecordId { get; set; }

    public string Question { get; set; }

    public string Answer { get; set; }

    public PhoneOrderIntent Intent { get; set; } = PhoneOrderIntent.Chat;
    
    public int Order { get; set; }
    
    public string ExtractFoodItem { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}