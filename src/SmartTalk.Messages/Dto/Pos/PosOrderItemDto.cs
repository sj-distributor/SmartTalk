using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Messages.Dto.Pos;

public class PosOrderItemDto : PhoneCallOrderItem
{
    public PosOrderItemStatus? Status { get; set; }
}