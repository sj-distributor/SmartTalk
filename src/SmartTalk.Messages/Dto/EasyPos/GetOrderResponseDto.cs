using Newtonsoft.Json;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Messages.Dto.EasyPos;

public class GetOrderRequestDto
{
    [JsonProperty("url")]
    public string BaseUrl { get; set; }
    
    [JsonProperty("appId")]
    public string AppId { get; set; }
    
    [JsonProperty("appSecret")]
    public string AppSecret { get; set; }
    
    [JsonProperty("orderId")]
    public long OrderId { get; set; }
}

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
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("orderItems")]
    public List<EasyPosOrderItemDto> OrderItems { get; set; }

    [JsonProperty("createAt")]
    public DateTimeOffset CreateAt { get; set; }
    
    [JsonProperty("type")]
    public int Type { get; set; }
    
    [JsonProperty("status")]
    public int Status { get; set; }
    
    [JsonProperty("notes")]
    public string Notes { get; set; }
    
    [JsonProperty("isTaxFree")]
    public bool IsTaxFree { get; set; }
    
    [JsonProperty("totalAmount")]
    public decimal TotalAmount { get; set; }
    
    [JsonProperty("subTotalAmount")]
    public decimal SubTotalAmount { get; set; }
    
    [JsonProperty("totalTaxAmount")]
    public decimal TotalTaxAmount { get; set; }
    
    [JsonProperty("customer")]
    public EasyPosOrderCustomerDto Customer { get; set; }

    [JsonProperty("isPrinted")]
    public bool IsPrinted { get; set; }
}

public class EasyPosOrderCustomerDto
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("phone")]
    public string Phone { get; set; }
    
    [JsonProperty("notes")]
    public string Notes { get; set; }
    
    [JsonProperty("addresses")]
    public List<EasyPosOrderCustomerAddressDto> Addresses { get; set; }
}

public class EasyPosOrderCustomerAddressDto
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("fullAddress")]
    public string FullAddress { get; set; }
    
    [JsonProperty("room")]
    public string Room { get; set; }
    
    [JsonProperty("associatedId")]
    public string AssociatedId { get; set; }
    
    [JsonProperty("addressType")]
    public int AddressType { get; set; }
    
    [JsonProperty("addressImg")]
    public string AddressImg { get; set; }
    
    [JsonProperty("city")]
    public string City { get; set; }
    
    [JsonProperty("state")]
    public string State { get; set; }
    
    [JsonProperty("postalCode")]
    public string PostalCode { get; set; }
    
    [JsonProperty("country")]
    public string Country { get; set; }
    
    [JsonProperty("line1")]
    public string Line1 { get; set; }
    
    [JsonProperty("line2")]
    public string Line2 { get; set; }
    
    [JsonProperty("lat")]
    public double Lat { get; set; }
    
    [JsonProperty("lng")]
    public double Lng { get; set; }
}

public class EasyPosOrderItemDto
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("orderId")]
    public long OrderId { get; set; }
    
    [JsonProperty("productId")]
    public long ProductId { get; set; }
    
    [JsonProperty("originalPrice")]
    public decimal OriginalPrice { get; set; }
    
    [JsonProperty("notes")]
    public string Notes { get; set; }
    
    [JsonProperty("price")]
    public decimal Price { get; set; }
    
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
    
    [JsonProperty("orderItemModifiers")]
    public List<EasyPosOrderItemModifiersDto> OrderItemModifiers { get; set; }
}

public class EasyPosOrderItemModifiersDto
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("orderId")]
    public long OrderId { get; set; }
    
    [JsonProperty("orderItemId")]
    public long OrderItemId { get; set; }
    
    [JsonProperty("modifierId")]
    public long ModifierId { get; set; }
    
    [JsonProperty("modifierProductId")]
    public long ModifierProductId { get; set; }
    
    [JsonProperty("productId")]
    public long ProductId { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [JsonProperty("price")]
    public decimal Price { get; set; }
    
    [JsonProperty("notes")]
    public string Notes { get; set; }
    
    [JsonProperty("sort")]
    public int Sort { get; set; }
    
    [JsonProperty("localizations")]
    public List<EasyPosLocalizationsDto> Localizations { get; set; }
    
    [JsonProperty("modifierLocalizations")]
    public List<EasyPosLocalizationsDto> ModifierLocalizations { get; set; }
    
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