using Autofac;
using Serilog;
using NSubstitute;
using MySqlConnector;
using SmartTalk.Core;
using Newtonsoft.Json;
using OpenAI.Interfaces;
using SmartTalk.Core.DbUpFile;
using SmartTalk.Core.Settings;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.IntegrationTests.Mocks;
using Microsoft.Extensions.Configuration;

namespace SmartTalk.IntegrationTests;

public partial class TestBase
{
    private readonly List<string> _tableRecordsDeletionExcludeList = new()
    {
        "schemaversions"
    };
    
    private void RegisterBaseContainer(ContainerBuilder containerBuilder)
    {
        var logger = Substitute.For<ILogger>();
        
        var configuration = RegisterConfiguration(containerBuilder);
        
        containerBuilder.RegisterModule(
            new SmartTalkModule(logger, configuration, typeof(SmartTalkModule).Assembly, typeof(TestBase).Assembly));
        
        containerBuilder.RegisterInstance(Substitute.For<IOpenAIService>()).AsImplementedInterfaces();
        
        RegisterSugarTalkBackgroundJobClient(containerBuilder);
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
    
    private void RunDbUpIfRequired()
    {
        if (!ShouldRunDbUpDatabases.GetValueOrDefault(_databaseName, true))
            return;

        new DbUpFileRunner(new SmartTalkConnectionString(CurrentConfiguration).Value).Run(nameof(Core.DbUpFile), typeof(DbUpFileRunner).Assembly);

        ShouldRunDbUpDatabases[_databaseName] = false;
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
    
    private void RegisterSugarTalkBackgroundJobClient(ContainerBuilder containerBuilder)
    {
        containerBuilder.RegisterType<MockingBackgroundJobClient>().As<ISmartTalkBackgroundJobClient>().InstancePerLifetimeScope();
    }
    
    public void Dispose()
    {
        ClearDatabaseRecord();
    }

    public async Task InitializeAsync()
    {
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}