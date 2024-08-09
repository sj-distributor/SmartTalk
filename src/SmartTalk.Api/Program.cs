using Autofac;
using Serilog;
using Destructurama;
using SmartTalk.Core;
using SmartTalk.Core.DbUp;
using SmartTalk.Core.Settings;
using SmartTalk.Core.Settings.Logging;
using Autofac.Extensions.DependencyInjection;
namespace SmartTalk.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();
        
        var apikey = new SerilogApiKeySetting(configuration).Value;
        var serverUrl = new SerilogServerUrlSetting(configuration).Value;
        var application = new SerilogApplicationSetting(configuration).Value;
        
        Log.Logger = new LoggerConfiguration()
            .Destructure.JsonNetTypes()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", application)
            .WriteTo.Console()
            .WriteTo.Seq(serverUrl, apiKey: apikey)
            .CreateLogger();
        
        try
        {
            Log.Information("Configuring api host ({ApplicationContext})...", application);
                
            new DbUpRunner(new SmartTalkConnectionString(configuration).Value).Run();
                
            var webHost = CreateHostBuilder(args, configuration).Build();

            Log.Information("Starting api host ({ApplicationContext})...", application);
                
            webHost.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Program terminated unexpectedly ({ApplicationContext})!", application);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    private static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configuration) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureLogging(l => l.AddSerilog(Log.Logger))
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                builder.RegisterModule(new SmartTalkModule(Log.Logger, configuration, typeof(SmartTalkModule).Assembly));
            })
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
}

