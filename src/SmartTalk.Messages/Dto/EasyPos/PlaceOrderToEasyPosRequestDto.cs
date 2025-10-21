using Newtonsoft.Json;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.EasyPos;

public class PlaceOrderToEasyPosRequestDto
{
    public int Id { get; set; }

    public int Type { get; set; } = 9;

    public int Guests { get; set; } = 1;

    public bool IsTaxFree { get; set; } = true;
    
    public string Notes { get; set; } = string.Empty;
    
    public int SourceType { get; set; } = 3;

    public List<PhoneCallOrderItem> OrderItems { get; set; } = [];
    
    public PhoneCallOrderCustomer Customer { get; set; }
}

public class PhoneCallOrderItem
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("productId")]
    public long ProductId { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [JsonProperty("originalPrice")]
    public decimal OriginalPrice { get; set; }
    
    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;
    
    [JsonProperty("orderItemModifiers")]
    public List<PhoneCallOrderItemModifiers> OrderItemModifiers { get; set; }
}

public class PhoneCallOrderItemModifiers
{
    [JsonProperty("price")]
    public decimal Price { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
    
    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;
    
    [JsonProperty("modifierId")]
    public long ModifierId { get; set; }
    
    [JsonProperty("modifierProductId")]
    public long ModifierProductId { get; set; }
    
    [JsonProperty("localizations")]
    public List<PhoneCallOrderItemLocalization> Localizations { get; set; }
    
    [JsonProperty("modifierLocalizations")]
    public List<PhoneCallOrderItemModifierLocalization> ModifierLocalizations { get; set; }
}

public class PhoneCallOrderItemLocalization
{
    [JsonProperty("field")]
    public string Field { get; set; }
    
    [JsonProperty("languageCode")]
    public string LanguageCode { get; set; }
    
    [JsonProperty("value")]
    public string Value { get; set; }
}

public class PhoneCallOrderCustomer
{
    public string Name { get; set; }
    
    public string Phone { get; set; }
    
    public List<PhoneCallOrderCustomerAddress> Addresses { get; set; }
}

public class PhoneCallOrderCustomerAddress
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("fullAddress")]
    public string FullAddress { get; set; }
    
    [JsonProperty("room")]
    public string Room { get; set; }
    
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

public class PhoneCallOrderItemModifierLocalization : PhoneCallOrderItemLocalization
{
}

public class PlaceOrderToEasyPosResponseDto : SmartTalkResponse<PlaceOrderToEasyPosResponseData>
{
    [JsonProperty("success")]
    public bool Success { get; set; }
}

public class PlaceOrderToEasyPosResponseData
{
    [JsonProperty("order")]
    public PlaceOrderToEasyPosResponseDataOrder Order { get; set; }
    
    [JsonProperty("orderSnapshot")]
    public object OrderSnapshot { get; set; }
}

public class PlaceOrderToEasyPosResponseDataOrder
{
    [JsonProperty("id")]
    public long Id { get; set; }
    
    [JsonProperty("status")]
    public int Status { get; set; }
    
    [JsonProperty("orderItems")]
    public List<PlaceOrderToEasyPosResponseDataOrderItem> OrderItems { get; set; }
}

public class PlaceOrderToEasyPosResponseDataOrderItem
{
    [JsonProperty("orderId")]
    public long OrderId { get; set; }
}

