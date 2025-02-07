using SmartTalk.Messages.Enums.Restaurants;

namespace SmartTalk.Messages.Dto.Restaurant;

public class RestaurantMenuItemDto
{
    public int Id { get; set; }

    public int RestaurantId { get; set; }
    
    public decimal Price { get; set; }
    
    public string Name { get; set; }
    
    public RestaurantItemLanguage Language { get; set; }
    
    public long? ProductId { get; set; }

    public string OrderItemModifiers { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}