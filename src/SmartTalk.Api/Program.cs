using Autofac;
using Serilog;
using Destructurama;
using SmartTalk.Core;
using SmartTalk.Core.DbUpFile;
using SmartTalk.Core.Settings;
using SmartTalk.Core.Settings.Logging;
using SmartTalk.Core.Services.RealtimeAiV2;
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
            // Mask the MiniMax TTS credential so it never reaches logs when the session context
            // (which carries RealtimeSessionOptions.TtsConfig) is destructured with {@Context}.
            .Destructure.ByTransforming<RealtimeAiTtsConfig>(c => new
            {
                c.ProviderType,
                c.Voice,
                c.ServiceUrl,
                c.TargetCodec,
                c.SampleRate,
                ApiKey = MaskApiKey(c.ApiKey),
                c.ProviderSpecificConfig
            })
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", application)
            .WriteTo.Console()
            .WriteTo.Seq(serverUrl, apiKey: apikey)
            .CreateLogger();
        
        try
        {
            Log.Information("Configuring api host ({ApplicationContext})...", application);
                
            new DbUpFileRunner(new SmartTalkConnectionString(configuration).Value).Run(nameof(Core.DbUpFile), typeof(DbUpFileRunner).Assembly);
                
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
    
    // Keeps the first/last 4 chars so ops can tell which key is configured, while hiding the body.
    // Fully masks anything short enough that first4+last4 would expose (most of) the secret.
    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return string.Empty;
        if (apiKey.Length <= 8) return "***";

        return $"{apiKey[..4]}***{apiKey[^4..]}";
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

