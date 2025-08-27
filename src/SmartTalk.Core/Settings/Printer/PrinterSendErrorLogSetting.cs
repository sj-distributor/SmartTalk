using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Printer;

public class PrinterSendErrorLogSetting : IConfigurationSetting
{
    public PrinterSendErrorLogSetting(IConfiguration configuration)
    {
        CloudPrinterSendErrorLogRobotUrl = configuration.GetValue<string>("CloudPrinterSendErrorLogRobot");
    }

    public string CloudPrinterSendErrorLogRobotUrl { get; set; }
}