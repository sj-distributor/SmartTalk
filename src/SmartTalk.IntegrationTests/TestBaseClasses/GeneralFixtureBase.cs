using Xunit;

namespace SmartTalk.IntegrationTests.TestBaseClasses;

[Collection("General Tests")]
public class GeneralFixtureBase : TestBase
{
    protected GeneralFixtureBase() : base("_general_", "smart_talk_general", 0)
    {
    }
}