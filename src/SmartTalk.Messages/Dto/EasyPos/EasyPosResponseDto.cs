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
    
    [JsonProperty("timePeriods")]
    public List<EasyPosResponseTimePeriods> TimePeriods { get; set; }
    
    [JsonProperty("menus")]
    public List<EasyPosResponseMenu> Menus { get; set; }
}

public class EasyPosResponseProduct
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }
    
    [JsonProperty("menuIds")]
    public List<long> MenuIds { get; set; } 

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
    
    [JsonProperty("minimumSelect")]
    public int MinimumSelect { get; set; }
    
    [JsonProperty("maximumSelect")]
    public int MaximumSelect { get; set; }
    
    [JsonProperty("MaximumRepetition")]
    public int MaximumRepetition { get; set; }

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

public class EasyPosResponseTimePeriods
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("dayOfWeeks")]
    public List<int> DayOfWeeks { get; set; }

    [JsonProperty("startTime")]
    public string StartTime { get; set; }

    [JsonProperty("endTime")]
    public string EndTime { get; set; }
}

public class EasyPosResponseMenu
{
    [JsonProperty("menuId")]
    public long MenuId { get; set; }
    
    [JsonProperty("timePeriods")]
    public List<EasyPosResponseTimePeriods> TimePeriods { get; set; }
}