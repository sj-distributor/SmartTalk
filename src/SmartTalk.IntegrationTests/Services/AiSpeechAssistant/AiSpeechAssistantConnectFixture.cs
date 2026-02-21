using Autofac;
using NSubstitute;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.IntegrationTests.TestBaseClasses;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantConnectFixture : AiSpeechAssistantFixtureBase
{
    private const string TestCallerNumber = "+15551234567";
    private const string TestDidNumber = "+15559876543";
    private const string TestHost = "test.example.com";

    private static Action<ContainerBuilder> MockExternalServices => builder =>
    {
        builder.RegisterInstance(Substitute.For<ISmartiesClient>()).AsImplementedInterfaces();
    };
}
