using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Jobs;

public class CustomerItemsRefreshBatchSizeSetting : IConfigurationSetting<int>
{
    private const int DefaultBatchSize = 10;

    public CustomerItemsRefreshBatchSizeSetting(IConfiguration configuration)
    {
        var configuredValue = configuration.GetValue<int>("CustomerItemsRefreshBatchSize", DefaultBatchSize);
        Value = Math.Max(1, configuredValue);
    }

    public int Value { get; set; }
}
