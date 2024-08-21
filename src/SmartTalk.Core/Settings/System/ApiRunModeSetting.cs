using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Settings;
using SmartTalk.Messages.Enums.System;

namespace Smarties.Core.Settings.System;

public class ApiRunModeSetting : IConfigurationSetting<ApiRunMode>
{
    public ApiRunModeSetting(IConfiguration configuration)
    {
        Value = Enum.Parse<ApiRunMode>(configuration.GetValue<string>("ApiRunMode"), true);
    }
    
    public ApiRunMode Value { get; set; }
}