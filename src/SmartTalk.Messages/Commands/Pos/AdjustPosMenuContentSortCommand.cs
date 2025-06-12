using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class AdjustPosMenuContentSortCommand : ICommand
{
    public int ActiveId { get; set; }
    
    public int PassiveId { get; set; }
    
    public PosMenuContentType Type { get; set; }
}

public class AdjustPosMenuContentSortResponse : SmartTalkResponse;