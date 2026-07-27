using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.AiSpeechAssistant;

public class AiSpeechAssistantSettings : IConfigurationSetting
{
    public AiSpeechAssistantSettings(IConfiguration configuration)
    {
        EngineVersion = configuration.GetValue<int>("AiSpeechAssistant:EngineVersion");
        HifoodCapabilityCompanyIds = ParseCompanyIds(configuration);
    }

    public int EngineVersion { get; set; }

    public List<int> HifoodCapabilityCompanyIds { get; set; } = [];

    public bool CanConfigureHifoodCapabilities(int companyId)
    {
        return HifoodCapabilityCompanyIds.Contains(companyId);
    }

    private static List<int> ParseCompanyIds(IConfiguration configuration)
    {
        var section = configuration.GetSection("AiSpeechAssistant:HifoodCapabilityCompanyIds");

        if (!string.IsNullOrWhiteSpace(section.Value))
            return ParseCompanyIds(section.Value);

        var ids = section.Get<List<int>>();
        return ids is { Count: > 0 }
            ? ids.Distinct().ToList()
            : [];
    }

    private static List<int> ParseCompanyIds(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
            .Where(x => x.HasValue)
            .Select(x => x.Value)
            .Distinct()
            .ToList();
    }
}
