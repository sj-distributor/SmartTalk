using DbUp;
using System.Reflection;
using DbUp.ScriptProviders;
using MySql.Data.MySqlClient;
using Serilog;

namespace SmartTalk.Core.DbUpFile;

public class DbUpFileRunner
{
    private readonly string _connectionString;

    public DbUpFileRunner(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Run(string scriptFolderName, Assembly assembly)
    {
        CreateDatabaseIfNotExist(_connectionString);
        
        EnsureDatabase.For.MySqlDatabase(_connectionString);

        var upgradeEngine = DeployChanges.To.MySqlDatabase(_connectionString)
            .WithScriptsFromFileSystem(
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, scriptFolderName),
                new FileSystemScriptOptions
                {
                    IncludeSubDirectories = true
                })
            .WithScriptsAndCodeEmbeddedInAssembly(assembly, s => s.StartsWith($"{assembly.GetName().Name}.{scriptFolderName}"))
            .WithTransaction()
            .LogToAutodetectedLog()
            .LogToConsole()
            .Build();

        var result = upgradeEngine.PerformUpgrade();

        if (!result.Successful)
        {
            if (result.ErrorScript != null)
            {
                Log.Error("DbUp failed on script: {ErrorScriptName}", result.ErrorScript.Name);
                Log.Error("DbUp failed on script content: {ErrorScriptContent}", result.ErrorScript.Contents);
                
                Console.WriteLine($"DbUp failed on script: {result.ErrorScript.Name}");
                Console.WriteLine(result.ErrorScript.Contents);
            }

            throw result.Error;
        }
    }
    
    private void CreateDatabaseIfNotExist(string connectionStr)
    {
        var (connectionString, databaseName) = GetConnectionStringThatWithoutConnectingToAnyDatabase(connectionStr);

        using var connection = new MySqlConnection(connectionString);

        using var command = new MySqlCommand(
            "CREATE SCHEMA If Not Exists `" + databaseName + "` Character Set UTF8;", connection);

        try
        {
            connection.Open();
            command.ExecuteNonQuery();
        }
        finally
        {
            connection.Close();
        }
    }
    
    private (string ConnectionString, string DatabaseName) GetConnectionStringThatWithoutConnectingToAnyDatabase(string connectionStr)
    {
        var connectionStringBuilder = new MySqlConnectionStringBuilder(connectionStr);
        var database = connectionStringBuilder.Database;
        connectionStringBuilder.Database = null;
        return (connectionStringBuilder.ToString(), database);
    }
}