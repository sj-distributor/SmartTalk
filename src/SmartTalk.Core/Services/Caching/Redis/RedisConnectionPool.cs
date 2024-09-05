using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Caching;
using SmartTalk.Core.Settings.VectorDb;
using SmartTalk.Messages.Enums.Caching;
using StackExchange.Redis;

namespace SmartTalk.Core.Services.Caching.Redis;

public interface IRedisConnectionPool : ISingletonDependency
{
    ConnectionMultiplexer GetConnection(RedisServer redisServer = RedisServer.System);
}

public class RedisConnectionPool : IRedisConnectionPool
{
    private readonly Dictionary<RedisServer, List<ConnectionMultiplexer>> _pool;

    public RedisConnectionPool(RedisCacheConnectionStringSetting connectionStringSetting, RedisCacheConnectionStringForVectorSetting connectionStringForVectorSetting)
    {
        _pool = new Dictionary<RedisServer, List<ConnectionMultiplexer>>
        {
            { RedisServer.System, new List<ConnectionMultiplexer>() },
            { RedisServer.Vector, new List<ConnectionMultiplexer>() }
        };
        
        InitializePool(RedisServer.System, connectionStringSetting.Value);
        InitializePool(RedisServer.Vector, connectionStringForVectorSetting.Value);
    }

    public ConnectionMultiplexer GetConnection(RedisServer redisServer = RedisServer.System)
    {
        return _pool.TryGetValue(redisServer, out var connections) && connections.Count > 0
            ? connections[new Random().Next(connections.Count)]
            : _pool[RedisServer.System].FirstOrDefault();
    }
    
    private void InitializePool(RedisServer server, string connectionString, int poolSize = 10)
    {
        for (var i = 0; i < poolSize; i++)
        {
            _pool[server].Add(ConnectionMultiplexer.Connect(connectionString));
        }
    }
}