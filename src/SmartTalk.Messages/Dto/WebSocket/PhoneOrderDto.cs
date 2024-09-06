namespace SmartTalk.Messages.Dto.WebSocket;

public class PhoneOrderDto
{
    public List<PhoneOrderConversationDetailDto> ConversationDetail { get; set; } = new();

    public List<PhoneOrderFoodItemDto> FoodItem { get; set; } = new();
}