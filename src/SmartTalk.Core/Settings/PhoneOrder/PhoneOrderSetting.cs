using System.Text.Json;
using Microsoft.Extensions.Configuration;
namespace SmartTalk.Core.Settings.PhoneOrder;

public class PhoneOrderSetting : IConfigurationSetting
{
    public PhoneOrderSetting(IConfiguration configuration)
    {
        Robots = JsonSerializer.Deserialize<Dictionary<string, string>>(configuration.GetValue<string>("PhoneOrder:Robots"));
    }

    public Dictionary<string, string> Robots { get; set; }
    
    public string GetSetting(string key) =>
        Robots.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Setting '{key}' not found.");
}