namespace SmartTalk.Messages.Dto.WebSocket;

public class PhoneOrderFoodItemDto
{
    public string SessionId { get; set; }
        
    public string FoodName { get; set; }
    
    public int Quantity { get; set; }
    
    public double Price { get; set; }
    
    public string Note { get; set; }
}