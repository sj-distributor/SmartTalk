using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.VectorDb;

public class VectorDbSettings : IConfigurationSetting
{
    public VectorDbSettings(IConfiguration configuration)
    {
        AppPrefix = configuration.GetValue<string>("RetrievalDb:VectorDb:AppPrefix");
        ConnectionString = configuration.GetValue<string>("SmartChat:Services:Redis:ConnectionString");
    }
    
    public string AppPrefix { get; set; }
    
    public string ConnectionString { get; set; }
}