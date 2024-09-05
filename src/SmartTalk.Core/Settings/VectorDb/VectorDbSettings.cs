using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.VectorDb;

public class VectorDbSettings : IConfigurationSetting
{
    public VectorDbSettings(IConfiguration configuration)
    {
        AppPrefix = configuration.GetValue<string>("VectorDb:AppPrefix");
    }
    
    public string AppPrefix { get; set; }
}