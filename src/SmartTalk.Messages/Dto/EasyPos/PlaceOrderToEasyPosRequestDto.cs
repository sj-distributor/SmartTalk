using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.EasyPos;

public class PlaceOrderToEasyPosRequestDto
{
    public int Id { get; set; }

    public int Type { get; set; } = 9;

    public bool IsTaxFree { get; set; } = true;
    
    public string Notes { get; set; } = string.Empty;

    public List<PhoneCallOrderItem> OrderItems { get; set; } = [];
}

public class PhoneCallOrderItem
{
    public int Id { get; set; }
    
    public long ProductId { get; set; }
    
    public int Quantity { get; set; }
    
    public double OriginalPrice { get; set; }
    
    public double Price { get; set; }

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

public class PhoneCallOrderItemModifierLocalization : PhoneCallOrderItemLocalization
{
}

public class PlaceOrderToEasyPosResponseDto : SmartTalkResponse<PlaceOrderToEasyPosResponseData>
{
    public bool Success { get; set; }
}

public class PlaceOrderToEasyPosResponseData
{
    public PlaceOrderToEasyPosResponseDataOrder Order { get; set; }
    
    public object OrderSnapshot { get; set; }
}

public class PlaceOrderToEasyPosResponseDataOrder
{
    public int Status { get; set; }
    
    public List<object> OrderItems { get; set; }
}

