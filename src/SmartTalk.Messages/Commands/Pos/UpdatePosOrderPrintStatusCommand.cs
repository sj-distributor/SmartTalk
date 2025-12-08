using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosOrderPrintStatusCommand : ICommand
{
    public int StoreId { get; set; }

    public string OrderId { get; set; }

    public int RetryCount { get; set; }
}

public class UpdatePosOrderPrintStatusResponse : SmartTalkResponse<PosOrderDto>
{
}