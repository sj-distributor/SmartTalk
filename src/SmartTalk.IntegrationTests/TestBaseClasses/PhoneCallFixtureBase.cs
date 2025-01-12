using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("Phone Order Tests")]
public class PhoneCallFixtureBase : TestBase
{
    protected PhoneCallFixtureBase() : base("_phone_order_", "phone_order", 1)
    {
    }
}