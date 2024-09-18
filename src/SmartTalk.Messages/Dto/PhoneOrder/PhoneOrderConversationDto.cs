using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneOrder;
using System.ComponentModel.DataAnnotations.Schema;

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

    public List<ExtractFoodItemDto> ExtractFoodItemOject { get; set; }
    
    public string ExtractFoodItem => JsonConvert.SerializeObject( ExtractFoodItemOject );
    
    public DateTimeOffset CreatedDate { get; set; }
}

public class ExtractFoodItemDto
{
    public int Count { get; set; }
    
    public double Price { get; set; }
    
    public string Remark { get; set; }

    public string FoodItem { get; set; }
}