using Xunit;
using Autofac;
using NSubstitute;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Services.Infrastructure;

namespace SmartTalk.IntegrationTests;

public partial class TestBase : TestUtilBase, IAsyncLifetime, IDisposable
{
    private readonly string _testTopic;
    private readonly string _databaseName;
    
    private static readonly ConcurrentDictionary<string, IContainer> Containers = new();

    private static readonly ConcurrentDictionary<string, bool> ShouldRunDbUpDatabases = new();

    protected ILifetimeScope CurrentScope { get; }

    protected IConfiguration CurrentConfiguration => CurrentScope.Resolve<IConfiguration>();

    protected TestBase(string testTopic, string databaseName)
    {
        _testTopic = testTopic;
        _databaseName = databaseName;
        var root = Containers.GetValueOrDefault(testTopic);

        if (root == null)
        {
            var containerBuilder = new ContainerBuilder();
            RegisterBaseContainer(containerBuilder);
            root = containerBuilder.Build();
            Containers[testTopic] = root;
        }

        CurrentScope = root.BeginLifetimeScope();

        RunDbUpIfRequired();
        SetupScope(CurrentScope);
    }
    
    protected IClock MockClock(ContainerBuilder builder, DateTimeOffset? mockedDate = null)
    {
        mockedDate ??= DateTimeOffset.Now;
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(mockedDate.Value);
        builder.Register(_ => clock);
        return clock;
    }
}