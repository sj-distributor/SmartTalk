using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.FilesSynchronize;

public class FilesSynchronizeSetting : IConfigurationSetting
{
    public FilesSynchronizeSetting(IConfiguration configuration)
    {
        Source = configuration.GetValue<string>("FilesSynchronize:Source");
        Destinations = configuration.GetValue<string>("FilesSynchronize:Destinations").Split(',').ToList();
        PrivateKey = configuration.GetValue<string>("FilesSynchronize:PrivateKey");
    }
    
    public string Source { get; set; }
    
    public List<string> Destinations { get; set; }
    
    public string PrivateKey { get; set; }
}