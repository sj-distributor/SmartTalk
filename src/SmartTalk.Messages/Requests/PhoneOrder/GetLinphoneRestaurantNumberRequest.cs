using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetLinphoneRestaurantNumberRequest : IRequest
{
    public string ToRestaurant { get; set; }
}

public class GetLinphoneRestaurantNumberResponse : SmartTalkResponse<string>
{
}