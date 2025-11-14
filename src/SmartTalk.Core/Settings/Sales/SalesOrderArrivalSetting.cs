using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.Sales;

public class SalesOrderArrivalSetting : IConfigurationSetting
{
    public SalesOrderArrivalSetting(IConfiguration configuration)
    {
        ApiKey = configuration.GetValue<string>("SalesOrderArrival:ApiKey");

        BaseUrl = configuration.GetValue<string>("SalesOrderArrival:BaseUrl");

        Organizationid = configuration.GetValue<string>("SalesOrderArrival:OrganizationId");
    }

    public string ApiKey { get; set; }

    public string BaseUrl { get; set; }

    public string Organizationid { get; set; }
}