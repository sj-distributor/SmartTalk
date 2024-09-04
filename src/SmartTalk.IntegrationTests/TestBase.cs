using Xunit;
using Autofac;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using SmartTalk.IntegrationTests.Utils.Account;
using StackExchange.Redis;

namespace SmartTalk.IntegrationTests;

public partial class TestBase : TestUtilBase, IAsyncLifetime, IDisposable
{
    private readonly string _testTopic;
    private readonly string _databaseName;
    private readonly int _redisDatabaseIndex;
    
    private readonly IdentityUtil _identityUtil;
    
    private static readonly ConcurrentDictionary<string, IContainer> Containers = new();

    private static readonly ConcurrentDictionary<string, bool> ShouldRunDbUpDatabases = new();
    
    private static readonly ConcurrentDictionary<int, ConnectionMultiplexer> RedisPool = new();
    
    private static readonly ConcurrentDictionary<int, ConnectionMultiplexer> RedisStackPool = new();

    protected ILifetimeScope CurrentScope { get; }

    protected IConfiguration CurrentConfiguration => CurrentScope.Resolve<IConfiguration>();

    protected TestBase(string testTopic, string databaseName, Action<ContainerBuilder>? extraRegistration = null)
    {
        _testTopic = testTopic;
        _databaseName = databaseName;
        var root = Containers.GetValueOrDefault(testTopic);

        if (root == null)
        {
            var containerBuilder = new ContainerBuilder();
            RegisterBaseContainer(containerBuilder);
            extraRegistration?.Invoke(containerBuilder);
            root = containerBuilder.Build();
            Containers[testTopic] = root;
        }

        CurrentScope = root.BeginLifetimeScope();

        RunDbUpIfRequired();
        SetupScope(CurrentScope);
    
        _identityUtil = new IdentityUtil(CurrentScope);
    }
}