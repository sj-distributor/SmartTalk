using Microsoft.Extensions.Configuration;

namespace SmartTalk.Core.Settings.EasyPos;

public class EasyPosSetting : IConfigurationSetting
{
    public EasyPosSetting(IConfiguration configuration)
    {
        BaseUrl = configuration.GetValue<string>("EasyPos:BaseUrl");
        Authorizations = configuration.GetValue<string>("EasyPos:Authorizations").Split(',').ToList();
        MerchantIds = configuration.GetValue<string>("EasyPos:MerchantIds").Split(',').ToList();
        CompanyIds = configuration.GetValue<string>("EasyPos:CompanyIds").Split(',').ToList();
        MerchantStaffIds = configuration.GetValue<string>("EasyPos:MerchantStaffIds").Split(',').ToList();
        
        MoonHousePosOrderAuthorization = configuration.GetValue<string>("EasyPos:PosOrder:MoonHouse:Authorization");
        MoonHousePosOrderMerchantId = configuration.GetValue<string>("EasyPos:PosOrder:MoonHouse:MerchantId");
        MoonHousePosOrderCompanyId = configuration.GetValue<string>("EasyPos:PosOrder:MoonHouse:CompanyId");
        MoonHousePosOrderMerchantStaffId = configuration.GetValue<string>("EasyPos:PosOrder:MoonHouse:MerchantStaffId");
    }
    
    public string BaseUrl { get; set; }
    
    public List<string> Authorizations { get; set; }
    
    public List<string> MerchantIds { get; set; }
        
    public List<string> CompanyIds { get; set; }
    
    public List<string> MerchantStaffIds { get; set; }
    
    public string MoonHousePosOrderAuthorization { get; set; }
    
    public string MoonHousePosOrderMerchantId { get; set; }
    
    public string MoonHousePosOrderCompanyId { get; set; }
    
    public string MoonHousePosOrderMerchantStaffId { get; set; }
}