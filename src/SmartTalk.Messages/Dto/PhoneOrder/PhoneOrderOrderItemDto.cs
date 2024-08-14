using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderOrderItemDto
{
    public int Id { get; set; }
    
    public int RecordId { get; set; }

    public string FoodName { get; set; }

    public int Quantity { get; set; }

    public double Price { get; set; }

    public string Note { get; set; }

    public PhoneOrderOrderType OrderType { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}