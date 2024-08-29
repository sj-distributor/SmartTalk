using System.Text.Json;
using Microsoft.Extensions.Configuration;
namespace SmartTalk.Core.Settings.PhoneOrder;

public class PhoneOrderSetting : IConfigurationSetting
{
    public PhoneOrderSetting(){}
    public PhoneOrderSetting(IConfiguration configuration)
    {
        Robots = JsonSerializer.Deserialize<Dictionary<string, string>>(configuration.GetValue<string>("PhoneOrder:Robots"));
        AuthHeaders = configuration.GetValue<List<string>>("TranscriptionCallback:AuthHeaders");
        Url = configuration.GetValue<string>("TranscriptionCallback:Url");
    }

    public Dictionary<string, string> Robots { get; set; }
    
    public List<string> AuthHeaders { get; set; }
    
    public string Url { get; set; }
    
    public string GetSetting(string key) =>
        Robots.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Setting '{key}' not found.");
}