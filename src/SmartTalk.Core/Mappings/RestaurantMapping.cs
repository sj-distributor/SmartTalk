using AutoMapper;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Dto.Restaurant;

namespace SmartTalk.Core.Mappings;

public class RestaurantMapping : Profile
{
    public RestaurantMapping()
    {
        CreateMap<Restaurant, RestaurantDto>().ReverseMap();
        CreateMap<RestaurantMenuItem, RestaurantMenuItemDto>().ReverseMap();
        CreateMap<RestaurantMenuItem, RestaurantPayloadDto>().ReverseMap();
    }
}