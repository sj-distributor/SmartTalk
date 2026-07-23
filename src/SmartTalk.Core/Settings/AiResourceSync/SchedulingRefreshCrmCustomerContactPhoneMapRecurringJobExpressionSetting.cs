using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.AiResourceSync;

public class SchedulingRefreshCrmCustomerContactPhoneMapRecurringJobCronExpression : IConfigurationSetting<string>
{
    public SchedulingRefreshCrmCustomerContactPhoneMapRecurringJobCronExpression(IConfiguration configuration)
    {
        Value = configuration.GetValue<string>("SchedulingRefreshCrmCustomerContactPhoneMapRecurringJobCronExpression");
    }

    public string Value { get; set; }
}
