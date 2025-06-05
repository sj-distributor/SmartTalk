using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosCategoryCommand : ICommand
{
    public int Id { get; set; }
    
    public string Names { get; set; }
}

public class UpdatePosCategoryResponse : SmartTalkResponse<List<PosCategoryDto>>
{
}