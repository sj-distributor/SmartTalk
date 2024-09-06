namespace SmartTalk.Messages.Dto.Restaurant;

public class RestaurantMenuItemDto
{
    public int Id { get; set; }

    public int RestaurantId { get; set; }
    
    public decimal Price { get; set; }
    
    public string NameEn { get; set; }
    
    public string NameZh { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}