using Newtonsoft.Json;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Messages.Dto.Pos;

public class PosOrderItemDto : PhoneCallOrderItem
{
    [JsonProperty("status")]
    public PosOrderItemStatus? Status { get; set; }
}