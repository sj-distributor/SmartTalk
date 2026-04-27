using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Qwen;

public class QwenSettings : IConfigurationSetting
{
    private readonly IReadOnlyList<string> _crmBaseUrls;

    public QwenSettings(IConfiguration configuration)
    {
        _crmBaseUrls = ReadBaseUrls(configuration, "Qwen:Crm:BaseUrl");
        CrmApiKey = configuration.GetValue<string>("Qwen:Crm:ApiKey");
        CrmModel = configuration.GetValue<string>("Qwen:Crm:Model");
    }

    public IReadOnlyList<string> CrmBaseUrls => _crmBaseUrls;

    public string CrmBaseUrl
    {
        get
        {
            if (_crmBaseUrls.Count == 0)
            {
                return string.Empty;
            }

            if (_crmBaseUrls.Count == 1)
            {
                return _crmBaseUrls[0];
            }

            return _crmBaseUrls[Random.Shared.Next(_crmBaseUrls.Count)];
        }
    }
    public string CrmApiKey { get; set; }
    
    public string CrmModel { get; set; }

    private static IReadOnlyList<string> ReadBaseUrls(IConfiguration configuration, string key)
    {
        var fromValue = configuration.GetValue<string>(key);
        if (string.IsNullOrWhiteSpace(fromValue))
        {
            return Array.Empty<string>();
        }

        // Accept a single URL or comma-separated URLs.
        return fromValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }
}