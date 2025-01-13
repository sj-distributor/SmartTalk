using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneCall;

namespace SmartTalk.Messages.Dto.EasyPos;

public class GetOrderResponse
{
    [JsonProperty("code")]
    public string Code { get; set; }
    
    [JsonProperty("msg")]
    public string Msg { get; set; }
    
    [JsonProperty("data")]
    public GetOrderResponseDto Data { get; set; }
    
    [JsonProperty("success")]
    public bool Success { get; set; }
}

public class GetOrderResponseDto
{
    [JsonProperty("order")]
    public EasyPosOrderDto Order { get; set; }
}

public class EasyPosOrderDto
{
    [JsonProperty("orderItems")]
    public List<EasyPosOrderItemDto> OrderItems { get; set; }

    [JsonProperty("createAt")]
    public DateTimeOffset CreateAt { get; set; }
}

public class EasyPosOrderItemDto
{
    [JsonProperty("orderId")]
    public long OrderId { get; set; }
    
    [JsonProperty("menuId")]
    public long MenuId { get; set; }

    [JsonProperty("categoryId")]
    public long CategoryId { get; set; }

    [JsonProperty("itemAmount")]
    public double ItemAmount { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("localizations")]
    public List<EasyPosLocalizationsDto> Localizations { get; set; }
    
    [JsonProperty("condiments")]
    public List<EasyPosCondimentsDto> Condiments { get; set; }
}

public class EasyPosLocalizationsDto
{
    [JsonProperty("field")]
    public string Field { get; set; }

    [JsonProperty("languageCode")]
    public string languageCode { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; }
}

public class EasyPosCondimentsDto
{
    [JsonProperty("price")] 
    public decimal Price { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [JsonProperty("notes")]
    public string Notes { get; set; }
    
    [JsonProperty("localizations")]
    public List<EasyPosLocalizationsDto> Localizations { get; set; }
    
    [JsonProperty("actionLocalizations")]
    public List<EasyPosLocalizationsDto> ActionLocalizations { get; set; }
}