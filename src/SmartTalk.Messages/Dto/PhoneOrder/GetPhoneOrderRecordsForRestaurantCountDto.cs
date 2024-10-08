using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class GetPhoneOrderRecordsForRestaurantCountDto
{
    public PhoneOrderRestaurant Restaurant { get; set; }

    public List<RestaurantCountDto> Classes { get; set; }
}

public class RestaurantCountDto
{
    public string TimeFrame { get; set; }
    
    public int Count { get; set; }
}