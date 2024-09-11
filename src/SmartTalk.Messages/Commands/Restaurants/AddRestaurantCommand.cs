using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Restaurants;

public class AddRestaurantCommand : ICommand
{
    public string RestaurantName { get; set; }
}