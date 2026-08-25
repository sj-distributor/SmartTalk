using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.PhoneOrder;

public class AixvolinkPhoneOrderSetting : IConfigurationSetting
{
    public AixvolinkPhoneOrderSetting(IConfiguration configuration)
    {
        DefaultAgentId = configuration.GetValue<int?>("AixvolinkPhoneOrder:DefaultAgentId");
        DefaultAssistantId = configuration.GetValue<int?>("AixvolinkPhoneOrder:DefaultAssistantId");
    }

    public int? DefaultAgentId { get; set; }

    public int? DefaultAssistantId { get; set; }
}
