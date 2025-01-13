using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.PhoneCall;

public class PhoneCallSetting : IConfigurationSetting
{
    public PhoneCallSetting(){}
    public PhoneCallSetting(IConfiguration configuration)
    {
        Robots = JsonSerializer.Deserialize<Dictionary<string, string>>(configuration.GetValue<string>("PhoneCall:Robots"));
    }

    public Dictionary<string, string> Robots { get; set; }
    
    public string GetSetting(string key) =>
        Robots.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Setting '{key}' not found.");
}