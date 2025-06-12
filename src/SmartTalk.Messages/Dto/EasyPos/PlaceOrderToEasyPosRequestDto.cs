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
    public int Id { get; set; }
    
    public long ProductId { get; set; }
    
    public int Quantity { get; set; }
    
    public decimal OriginalPrice { get; set; }
    
    public decimal Price { get; set; }

    public string Notes { get; set; } = string.Empty;
    
    public List<PhoneCallOrderItemModifiers> OrderItemModifiers { get; set; }
}

public class PhoneCallOrderItemModifiers
{
    public double Price { get; set; }
    
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

