namespace SmartTalk.Core.Settings;

public interface IConfigurationSetting
{
}

public interface IConfigurationSetting<TValue> : IConfigurationSetting
{
    TValue Value { get; set; }
}