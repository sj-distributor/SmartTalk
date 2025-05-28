using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.EasyPos;

public class EasyPosResponseDto
{
    [JsonProperty("code")]
    public string Code { get; set; }
    
    [JsonProperty("msg")]
    public string Msg { get; set; }
    
    [JsonProperty("data")]
    public EasyPosResponseData Data { get; set; }
    
    [JsonProperty("success")]
    public bool Success { get; set; }
}

public class EasyPosResponseData
{
    [JsonProperty("products")]
    public List<EasyPosResponseProduct> Products { get; set; }
    
    [JsonProperty("menus")]
    public List<EasyPosResponseMenu> Menus { get; set; }
    
    [JsonProperty("categories")]
    public List<StoreCategory> Categories { get; set; }
    
    [JsonProperty("promotionProducts")]
    public List<EasyPosResponseProduct> PromotionProducts { get; set; }
    
    [JsonProperty("promotionCategories")]
    public List<StoreCategory> PromotionCategories { get; set; }
}

public class EasyPosResponseProduct
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("isIndependentSale")]
    public bool IsIndependentSale { get; set; }

    [JsonProperty("localizations")]
    public List<EasyPosResponseLocalization> Localizations { get; set; }

    [JsonProperty("modifierGroups")]
    public List<EasyPosResponseModifierGroups> ModifierGroups { get; set; }
}

public class EasyPosResponseLocalization
{
    [JsonProperty("field")]
    public string Field { get; set; }

    [JsonProperty("languageCode")]
    public string LanguageCode { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; }
}

public class EasyPosResponseModifierGroups
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("localizations")]
    public List<EasyPosResponseLocalization> Localizations { get; set; }
    
    [JsonProperty("modifierProducts")]
    public List<EasyPosResponseModifierProducts> ModifierProducts { get; set; }
}

public class EasyPosResponseModifierProducts
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("price")]
    public decimal Price { get; set; }
    
    [JsonProperty("localizations")]
    public List<EasyPosResponseLocalization> Localizations { get; set; }
}

public class EasyPosResponseMenu
{
    [JsonProperty("id")]
    public int Id { get; set; }
    
    [JsonProperty("companyId")]
    public int CompanyId { get; set; }
    
    [JsonProperty("merchantId")]
    public int MerchantId { get; set; }
    
    [JsonProperty("coefficient")]
    public int Coefficient { get; set; }
    
    [JsonProperty("desc")]
    public string Desc { get; set; }

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("isAvailable")]
    public bool IsAvailable { get; set; }

    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("timePeriods")]
    public List<StoreTimePeriodInfo> TimePeriods { get; set; }

    [JsonProperty("promotionCategories")]
    public List<StoreCategory> PromotionCategories { get; set; }

    [JsonProperty("categories")]
    public List<StoreCategory> Categories { get; set; }

    [JsonProperty("timePeriodIds")]
    public List<int> TimePeriodIds { get; set; }

    [JsonProperty("promotionCategoryIds")]
    public List<int> PromotionCategoryIds { get; set; }

    [JsonProperty("categoryIds")]
    public List<int> CategoryIds { get; set; }
}

public class StoreTax
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("associatedId")]
    public int AssociatedId { get; set; }

    [JsonProperty("associatedType")]
    public int AssociatedType { get; set; }

    [JsonProperty("merchantId")]
    public int MerchantId { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("isPercentage")]
    public bool IsPercentage { get; set; }

    [JsonProperty("isSelectedByDefault")]
    public bool IsSelectedByDefault { get; set; }

    [JsonProperty("value")]
    public decimal Value { get; set; }
}

public class StoreTimePeriodInfo
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("desc")]
    public string Desc { get; set; }

    [JsonProperty("dayOfWeeks")]
    public List<int> DayOfWeeks { get; set; }

    [JsonProperty("startTime")]
    public StoreTimeSpanInfo StartTime { get; set; }

    [JsonProperty("endTime")]
    public StoreTimeSpanInfo EndTime { get; set; }

    [JsonProperty("updateAt")]
    public DateTime UpdateAt { get; set; }

    [JsonProperty("associatedId")]
    public int AssociatedId { get; set; }

    [JsonProperty("associatedType")]
    public int AssociatedType { get; set; }
}

public class StoreCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("desc")]
    public string Desc { get; set; }

    [JsonProperty("color")]
    public string Color { get; set; }

    [JsonProperty("companyId")]
    public int CompanyId { get; set; }

    [JsonProperty("merchantId")]
    public int MerchantId { get; set; }

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("sort")]
    public int Sort { get; set; }

    [JsonProperty("isDelete")]
    public bool IsDelete { get; set; }

    [JsonProperty("localizations")]
    public List<StoreTimePeriodInfo> Localizations { get; set; }

    [JsonProperty("products")]
    public List<EasyPosResponseProduct> Products { get; set; }

    [JsonProperty("menuIds")]
    public List<int> MenuIds { get; set; }
}