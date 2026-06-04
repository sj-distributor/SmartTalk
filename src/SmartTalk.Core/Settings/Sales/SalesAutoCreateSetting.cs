using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Sales;

public class SalesAutoCreateSetting : IConfigurationSetting
{
    public SalesAutoCreateSetting(IConfiguration configuration)
    {
        NotifyRobotUrl = configuration.GetValue<string>("SalesAutoCreate:NotifyRobotUrl");
        DefaultAssistantGreetings = configuration.GetValue<string>("SalesAutoCreate:DefaultAssistantGreetings");
    }

    public string NotifyRobotUrl { get; set; }

    public string DefaultAssistantGreetings { get; set; }
}
