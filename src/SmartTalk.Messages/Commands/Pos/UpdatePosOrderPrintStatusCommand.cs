using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosOrderPrintStatusCommand : ICommand
{
    public int StoreId { get; set; }

    public long OrderId { get; set; }
}

public class UpdatePosOrderPrintStatusResponse : SmartTalkResponse<PosOrderDto>
{
}