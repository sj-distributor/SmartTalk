using Newtonsoft.Json;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.EasyPos;

public class PlaceOrderToEasyPosRequestDto
{
    public int Id { get; set; }

    public int Type { get; set; } = 9;

    public bool IsTaxFree { get; set; } = true;
    
    public string Notes { get; set; } = string.Empty;

    public List<PhoneCallOrderItem> OrderItems { get; set; } = [];
    
    public PhoneCallOrderCustomer Customer { get; set; }
}

public class PhoneCallOrderItem
{
    public long Id { get; set; }
    
    public long ProductId { get; set; }
    
    public int Quantity { get; set; }
    
    public decimal OriginalPrice { get; set; }
    
    public decimal Price { get; set; }

    public string Notes { get; set; } = string.Empty;
    
    public List<PhoneCallOrderItemModifiers> OrderItemModifiers { get; set; }
}

public class PhoneCallOrderItemModifiers
{
    public decimal Price { get; set; }
    
    public int Quantity { get; set; }
    
    public long ModifierId { get; set; }
    
    public long ModifierProductId { get; set; }
    
    public List<PhoneCallOrderItemLocalization> Localizations { get; set; }
    
    public List<PhoneCallOrderItemModifierLocalization> ModifierLocalizations { get; set; }
}

public class PhoneCallOrderItemLocalization
{
    public string Field { get; set; }
    
    public string LanguageCode { get; set; }
    
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

    [JsonProperty("associatedId")]
    public long AssociatedId { get; set; } = 1;

    [JsonProperty("addressType")]
    public int AddressType { get; set; } = 1;
    
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

