using Microsoft.Extensions.Configuration;
using SmartTalk.Messages.Enums.Communication.PhoneCall;

namespace SmartTalk.Core.Settings.Communication.PhoneCall;

public class PhoneCallProvidersSetting : IConfigurationSetting<IEnumerable<PhoneCallProvider>>
{
    public PhoneCallProvidersSetting()
    {
    }
    
    public PhoneCallProvidersSetting(IConfiguration configuration)
    {
        var phoneCallProviders = configuration.GetValue<string>("PhoneCallProviders");
        
        Value = !string.IsNullOrEmpty(phoneCallProviders)
            ? phoneCallProviders.Split(',').Select(Enum.Parse<PhoneCallProvider>)
            : new List<PhoneCallProvider>();
    }

    public IEnumerable<PhoneCallProvider> Value { get; set; }
}