using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Messages.Dto.PhoneOrder;

[Table("phone_order_conversation")]
public class PhoneOrderConversationDto
{
    public int Id { get; set; }

    public int RecordId { get; set; }

    public string Question { get; set; }

    public string Answer { get; set; }

    public int Order { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}