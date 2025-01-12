using SmartTalk.Messages.Enums.PhoneCall;

namespace SmartTalk.Messages.Dto.PhoneCall;

public class GetPhoneCallRecordsForRestaurantCountDto
{
    public PhoneCallRestaurant Restaurant { get; set; }

    public List<RestaurantCountDto> Classes { get; set; }
}

public class RestaurantCountDto
{
    public string TimeFrame { get; set; }
    
    public int Count { get; set; }
}