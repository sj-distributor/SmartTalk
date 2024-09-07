using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.VectorDb;

public class VectorDbSettings : IConfigurationSetting
{
    public VectorDbSettings(IConfiguration configuration)
    {
        AppPrefix = configuration.GetValue<string>("VectorDb:AppPrefix");

        EmbeddingModelMaxTokenTotal = configuration.GetValue<int>("VectorDb:EmbeddingModelMaxTokenTotal");
    }
    
    public string AppPrefix { get; set; }
        
    public int EmbeddingModelMaxTokenTotal { get; set; }
}