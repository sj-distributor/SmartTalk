using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("Phone Order Tests")]
public class PhoneOrderFixtureBase : TestBase
{
    protected PhoneOrderFixtureBase() : base("_phone_order_", "phone_order")
    {
    }
}