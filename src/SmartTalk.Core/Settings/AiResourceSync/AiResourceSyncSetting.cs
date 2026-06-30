using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.AiResourceSync;

public class AiResourceSyncSetting : IConfigurationSetting
{
    public AiResourceSyncSetting(IConfiguration configuration)
    {
        NotifyRobotUrl = configuration.GetValue<string>("AiResourceSync:NotifyRobotUrl");
        DefaultAssistantGreetings = configuration.GetValue<string>("AiResourceSync:DefaultAssistantGreetings");
        ServiceProviderId = configuration.GetValue<int?>("AiResourceSync:ServiceProviderId");
    }

    public string NotifyRobotUrl { get; set; }

    public string DefaultAssistantGreetings { get; set; }

    public int? ServiceProviderId { get; set; }
}
