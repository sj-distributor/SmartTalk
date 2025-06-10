using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class AdjustPosMenuContentSortCommand : ICommand
{
    public int Id { get; set; }
    
    public int Sort { get; set; }
    
    public PosMenuContentType Type { get; set; }
}

public class AdjustPosMenuContentSortResponse : SmartTalkResponse;