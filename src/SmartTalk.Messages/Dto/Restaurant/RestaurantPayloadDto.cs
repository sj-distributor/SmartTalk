using SmartTalk.Messages.Enums.Restaurants;

namespace SmartTalk.Messages.Dto.Restaurant;

public class RestaurantPayloadDto
{
    public int Id { get; set; }

    public int RestaurantId { get; set; }
    
    public decimal Price { get; set; }
    
    public string Name { get; set; }
    
    public long? ProductId { get; set; }
    
    public RestaurantItemLanguage Language { get; set; }
}