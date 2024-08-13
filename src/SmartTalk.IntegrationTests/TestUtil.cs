using Autofac;

namespace SmartTalk.IntegrationTests;

public class TestUtil : TestUtilBase
{
    protected TestUtil(ILifetimeScope scope)
    {
        SetupScope(scope);
    }
}