using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("Phone Order Tests")]
public class PhoneOrderFixtureBase : TestBase
{
    protected PhoneOrderFixtureBase() : base("_phone_order_", "smart_talk_phone_order", 1)
    {
    }
}