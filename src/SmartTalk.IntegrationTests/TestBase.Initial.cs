using Autofac;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using NSubstitute;
using Serilog;
using SmartTalk.Core;
using SmartTalk.Core.DbUpFile;
using SmartTalk.Core.Services.AliYun;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Settings;
using SmartTalk.Core.Settings.Caching;
using SmartTalk.IntegrationTests.Mocks;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Enums.Caching;
using StackExchange.Redis;

namespace SmartTalk.IntegrationTests;

public partial class TestBase
{
    private readonly TestCurrentUser _testCurrentUser = new();

    private readonly List<string> _tableRecordsDeletionExcludeList = new()
    {
        "schemaversions"
    };
    
    public async Task InitializeAsync()
    {
        await _identityUtil.CreateUser(_testCurrentUser);
        await _securityUtil.AddPermissionsAndAssignToUserAsync(_testCurrentUser.Id.Value, SecurityStore.Permissions.AllPermissions);
    }

    private void RegisterBaseContainer(ContainerBuilder containerBuilder)
    {
        var logger = Substitute.For<ILogger>();
        
        var configuration = RegisterConfiguration(containerBuilder);
        
        containerBuilder.RegisterModule(
            new SmartTalkModule(logger, configuration, typeof(SmartTalkModule).Assembly, typeof(TestBase).Assembly));
        
        containerBuilder.RegisterInstance(new TestCurrentUser()).As<ICurrentUser>();
        containerBuilder.RegisterInstance(Substitute.For<IHttpContextAccessor>()).AsImplementedInterfaces();
        containerBuilder.RegisterInstance(Substitute.For<IAliYunOssService>()).AsImplementedInterfaces();
        containerBuilder.RegisterInstance(Substitute.For<IEasyPosClient>()).AsImplementedInterfaces();
        containerBuilder.RegisterInstance(new MemoryCache(new MemoryCacheOptions())).As<IMemoryCache>().SingleInstance();
        
        RegisterRedis(containerBuilder);
        RegisterSmartTalkBackgroundJobClient(containerBuilder);
    }
    
    private IConfiguration RegisterConfiguration(ContainerBuilder containerBuilder)
    {
        var targetJson = $"appsettings{_testTopic}.json";
        File.Copy("appsettings.json", targetJson, true);
        dynamic jsonObj = JsonConvert.DeserializeObject(File.ReadAllText(targetJson));
        jsonObj["ConnectionStrings"]["SmartTalkConnectionString"] =
            jsonObj["ConnectionStrings"]["SmartTalkConnectionString"].ToString()
                .Replace("Database=smart_talk", $"Database={_databaseName}");
        File.WriteAllText(targetJson, JsonConvert.SerializeObject(jsonObj));
        var configuration = new ConfigurationBuilder().AddJsonFile(targetJson).Build();
        containerBuilder.RegisterInstance(configuration).AsImplementedInterfaces();
        return configuration;
    }

    private void RegisterSmartTalkBackgroundJobClient(ContainerBuilder containerBuilder)
    {
        containerBuilder.RegisterType<MockingBackgroundJobClient>().As<ISmartTalkBackgroundJobClient>().InstancePerLifetimeScope();
    }
    
    private void RegisterRedis(ContainerBuilder builder)
    {
        builder.Register(cfx =>
        {
            if (RedisPool.ContainsKey(_redisDatabaseIndex))
                return RedisPool[_redisDatabaseIndex];
                
            var redisConnectionSetting = cfx.Resolve<RedisCacheConnectionStringSetting>();
                
            var connString = $"{redisConnectionSetting.Value},defaultDatabase={_redisDatabaseIndex}";

            var instance = ConnectionMultiplexer.Connect(connString);
            
            return RedisPool.GetOrAdd(_redisDatabaseIndex, instance);
            
        }).Keyed<ConnectionMultiplexer>(RedisServer.System).ExternallyOwned();
        
        builder.Register(cfx =>
        {
            if (RedisStackPool.ContainsKey(_redisDatabaseIndex))
                return RedisStackPool[_redisDatabaseIndex];
                
            var redisConnectionForVectorSetting = cfx.Resolve<RedisCacheConnectionStringForVectorSetting>();
                
            var connString = $"{redisConnectionForVectorSetting.Value},defaultDatabase={_redisDatabaseIndex}";

            var instance = ConnectionMultiplexer.Connect(connString);
            
            return RedisStackPool.GetOrAdd(_redisDatabaseIndex, instance);
            
        }).Keyed<ConnectionMultiplexer>(RedisServer.Vector).ExternallyOwned();
    }
    
    private void RunDbUpIfRequired()
    {
        if (!ShouldRunDbUpDatabases.GetValueOrDefault(_databaseName, true))
            return;

        new DbUpFileRunner(new SmartTalkConnectionString(CurrentConfiguration).Value).Run(nameof(Core.DbUpFile), typeof(DbUpFileRunner).Assembly);

        ShouldRunDbUpDatabases[_databaseName] = false;
    }
    
    private void FlushRedisDatabase()
    {
        try
        {
            if (!RedisPool.TryGetValue(_redisDatabaseIndex, out var redis)) return;
            
            foreach (var endpoint in redis.GetEndPoints())
            {
                var server = redis.GetServer(endpoint);
                    
                server.FlushDatabase(_redisDatabaseIndex);
            }
        }
        catch
        {
            // ignored
        }
    }
    
    private void FlushRedisStackDatabase()
    {
        try
        {
            if (!RedisStackPool.TryGetValue(_redisDatabaseIndex, out var redis)) return;
            
            foreach (var endpoint in redis.GetEndPoints())
            {
                var server = redis.GetServer(endpoint);
                    
                server.FlushDatabase(_redisDatabaseIndex);    
            }
        }
        catch
        {
            // ignored
        }
    }
    
    private void ClearDatabaseRecord()
    {
        try
        {
            var connection = new MySqlConnection(new SmartTalkConnectionString(CurrentConfiguration).Value);

            var deleteStatements = new List<string>();

            connection.Open();

            using var reader = new MySqlCommand(
                    $"SELECT table_name FROM INFORMATION_SCHEMA.tables WHERE table_schema = '{_databaseName}';",
                    connection)
                .ExecuteReader();

            deleteStatements.Add($"SET SQL_SAFE_UPDATES = 0");
            while (reader.Read())
            {
                var table = reader.GetString(0);

                if (!_tableRecordsDeletionExcludeList.Contains(table))
                {
                    deleteStatements.Add($"DELETE FROM `{table}`");
                }
            }

            deleteStatements.Add($"SET SQL_SAFE_UPDATES = 1");

            reader.Close();

            var strDeleteStatements = string.Join(";", deleteStatements) + ";";

            new MySqlCommand(strDeleteStatements, connection).ExecuteNonQuery();

            connection.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cleaning up data, {_testTopic}, {ex}");
        }
    }
    
    public void Dispose()
    {
        ClearDatabaseRecord();
        FlushRedisDatabase();
        FlushRedisStackDatabase();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}