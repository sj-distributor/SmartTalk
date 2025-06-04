using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosProductCommand : ICommand
{
    public int Id { get; set; }
    
    public string Names { get; set; }
}

public class UpdatePosProductResponse : SmartTalkResponse<List<PosProductDto>>
{
}