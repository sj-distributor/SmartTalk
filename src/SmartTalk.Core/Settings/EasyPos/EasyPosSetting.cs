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
        
        PosOrderAuthorization = configuration.GetValue<string>("EasyPos:PosOrder:Authorization");
        PosOrderMerchantId = configuration.GetValue<string>("EasyPos:PosOrder:MerchantId");
        PosOrderCompanyId = configuration.GetValue<string>("EasyPos:PosOrder:CompanyId");
        PosOrderMerchantStaffId = configuration.GetValue<string>("EasyPos:PosOrder:MerchantStaffId");
    }
    
    public string BaseUrl { get; set; }
    
    public List<string> Authorizations { get; set; }
    
    public List<string> MerchantIds { get; set; }
        
    public List<string> CompanyIds { get; set; }
    
    public List<string> MerchantStaffIds { get; set; }
    
    public string PosOrderAuthorization { get; set; }
    
    public string PosOrderMerchantId { get; set; }
    
    public string PosOrderCompanyId { get; set; }
    
    public string PosOrderMerchantStaffId { get; set; }
}