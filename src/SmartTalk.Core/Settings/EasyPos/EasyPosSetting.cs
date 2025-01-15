using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.EasyPos;

public class EasyPosSetting : IConfigurationSetting
{
    public EasyPosSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("EasyPos:BaseUrl");
        
        MoonHouseAuthorization = configuration.GetValue<string>("EasyPos:MoonHouse:Authorization");
        MoonHouseMerchantId = configuration.GetValue<string>("EasyPos:MoonHouse:MerchantId");
        MoonHouseCompanyId = configuration.GetValue<string>("EasyPos:MoonHouse:CompanyId");
        MoonHouseMerchantStaffId = configuration.GetValue<string>("EasyPos:MoonHouse:MerchantStaffId");
    }
    
    public string BaseUrl { get; set; }
    
    public string MoonHouseAuthorization { get; set; }
    
    public string MoonHouseMerchantId { get; set; }
    
    public string MoonHouseCompanyId { get; set; }
    
    public string MoonHouseMerchantStaffId { get; set; }
}