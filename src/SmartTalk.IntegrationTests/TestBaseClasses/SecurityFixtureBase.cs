using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("Security Tests")]
public class SecurityFixtureBase : TestBase
{
    protected SecurityFixtureBase() : base("_security_", "smart_talk_security", 3)
    {
    }
}