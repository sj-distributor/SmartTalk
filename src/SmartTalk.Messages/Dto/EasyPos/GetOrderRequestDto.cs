using Newtonsoft.Json;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.EasyPos;

public class GetOrderRequestDto
{
    public long Id { get; set; }

    public PhoneOrderRestaurant restaurant { get; set; }
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
    public OrderDto Order { get; set; }
}

public class OrderDto
{
    [JsonProperty("orderItems")]
    public List<OrderItemDto> OrderItems { get; set; }

    [JsonProperty("createAt")]
    public DateTimeOffset CreateAt { get; set; }
}

public class OrderItemDto
{
    [JsonProperty("orderId")]
    public long OrderId { get; set; }
    
    [JsonProperty("menuId")]
    public long MenuId { get; set; }

    [JsonProperty("categoryId")]
    public long CategoryId { get; set; }

    [JsonProperty("price")]
    public double Price { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("localizations")]
    public List<LocalizationsDto> Localizations { get; set; }
}

public class LocalizationsDto
{
    [JsonProperty("field")]
    public string Field { get; set; }

    [JsonProperty("languageCode")]
    public string languageCode { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; }
}