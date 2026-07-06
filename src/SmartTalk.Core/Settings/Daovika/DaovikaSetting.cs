using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Daovika;

public class DaovikaSetting : IConfigurationSetting
{
    public DaovikaSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("Daovika:BaseUrl");
        ApiKey = configuration.GetValue<string>("Daovika:ApiKey");
        SalesGroupTableId = configuration.GetValue<string>("Daovika:SalesGroupTableId");
    }

    public string BaseUrl { get; set; }

    public string ApiKey { get; set; }

    public string SalesGroupTableId { get; set; }
}
