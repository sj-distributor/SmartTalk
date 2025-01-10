using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class GetPhoneCallRecordsForRestaurantCountDto
{
    public PhoneOrderRestaurant Restaurant { get; set; }

    public List<RestaurantCountDto> Classes { get; set; }
}

public class RestaurantCountDto
{
    public string TimeFrame { get; set; }
    
    public int Count { get; set; }
}