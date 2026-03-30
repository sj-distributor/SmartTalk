using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.OpenAi;

public class OpenAiSettings : IConfigurationSetting
{
    public OpenAiSettings(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("OpenAi:BaseUrl");
        ApiKey = configuration.GetValue<string>("OpenAi:ApiKey");
        ApiKeySlave = configuration.GetValue<string>("OpenAi:ApiKeySlave");
        ApiKeySlave2 = configuration.GetValue<string>("OpenAi:ApiKeySlave2");
        
        HkBaseUrl = configuration.GetValue<string>("OpenAiForHk:BaseUrl");
        HkApiKey = configuration.GetValue<string>("OpenAiForHk:ApiKey");
        HkApiKeySlave = configuration.GetValue<string>("OpenAiForHk:ApiKeySlave");
        HkApiKeySlave2 = configuration.GetValue<string>("OpenAiForHk:ApiKeySlave2");
    }
    
    // Us 
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
    
    public string ApiKeySlave { get; set; }
    
    public string ApiKeySlave2 { get; set; }
    
    // Hk 
    public string HkBaseUrl { get; set; }
    
    public string HkApiKey { get; set; }
    
    public string HkApiKeySlave { get; set; }
    
    public string HkApiKeySlave2 { get; set; }

    public IReadOnlyList<string> GetApiKeyCandidates(bool isHk = false)
    {
        var candidates = isHk
            ? new[] { HkApiKey, HkApiKeySlave, HkApiKeySlave2 }
            : new[] { ApiKey, ApiKeySlave, ApiKeySlave2 };

        return candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public string GetPreferredApiKey(bool isHk = false)
    {
        return GetApiKeyCandidates(isHk).FirstOrDefault();
    }
}
