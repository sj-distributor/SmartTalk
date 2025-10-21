using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Printer;

public class ScheduleScanOfflinePrinterJobExpressionSetting : IConfigurationSetting
{
    public ScheduleScanOfflinePrinterJobExpressionSetting(IConfiguration configuration)
    {
        CronExpression = configuration.GetValue<string>("Printer:Cron");
    }

    public string CronExpression { get; set; } 
}