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
    public List<EasyPosResponsemodifierGroups> ModifierGroups { get; set; }
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

public class EasyPosResponsemodifierGroups
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("companyId")]
    public long CompanyId { get; set; }

    [JsonProperty("merchantId")]
    public long MerchantId { get; set; }

    [JsonProperty("productId")]
    public long ProductId { get; set; }
    
    [JsonProperty("minimumSelect")]
    public int MinimumSelect { get; set; }
    
    [JsonProperty("maximumSelect")]
    public int MaximumSelect { get; set; }
    
    [JsonProperty("maximumRepetition")]
    public int MaximumRepetition { get; set; }
    
    [JsonProperty("desc")]
    public string Desc { get; set; }
    
    [JsonProperty("sort")]
    public int Sort { get; set; }
    
    [JsonProperty("localizations")]
    public List<EasyPosResponseLocalization> Localizations { get; set; }
    
    [JsonProperty("modifierProducts")]
    public List<EasyPosResponseModifierProducts> ModifierProducts { get; set; }
}

public class EasyPosResponseModifierProducts
{
    
}