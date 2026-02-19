using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("Restaurant Tests")]
public class RestaurantFixtureBase : TestBase
{
    protected RestaurantFixtureBase() : base("_restaurant_", "smart_talk_phone_order", 2)
    {
    }
}